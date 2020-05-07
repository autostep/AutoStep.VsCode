using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Definitions;
using AutoStep.Elements;
using AutoStep.Elements.Interaction;
using AutoStep.Elements.Parts;
using AutoStep.Elements.Test;
using AutoStep.Execution;
using AutoStep.Language;
using AutoStep.Language.Position;
using AutoStep.Language.Test.Matching;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Intellisense completion handler for test files.
    /// </summary>
    public class TestCompletionHandler : TestHandler, ICompletionHandler
    {
        private readonly DocumentSelector documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.as",
            });

        private CompletionCapability? clientCapability;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestCompletionHandler"/> class.
        /// </summary>
        /// <param name="projectHost">The project host.</param>
        public TestCompletionHandler(IWorkspaceHost projectHost)
            : base(projectHost)
        {
        }

        /// <inheritdoc/>
        public CompletionRegistrationOptions GetRegistrationOptions()
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = documentSelector,
                TriggerCharacters = new[] { " " },
            };
        }

        /// <summary>
        /// Handles the completion request.
        /// </summary>
        /// <param name="request">Request details (contains document and position info).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completion result.</returns>
        public async Task<CompletionList?> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var pos = await GetPositionInfoAsync(request.TextDocument, request.Position, cancellationToken);

            CompletionList? completionList = null;

            if (pos is PositionInfo position)
            {
                if (TryGetStepReference(position, out var stepRef))
                {
                    // We are in a step reference.
                    // Get the possible step definitions for this step.
                    var possibleMatches = Workspace.GetPossibleStepDefinitions(stepRef);

                    var startInsertPos = request.Position;
                    Position endInsertPos = request.Position;

                    // Get the first token in the line that we will replace from.
                    var firstInsertToken = position.LineTokens.FirstOrDefault(t => t.Category == LineTokenCategory.StepText || t.Category == LineTokenCategory.Variable);

                    // Get the last position of provided content.
                    var lastInsertToken = position.LineTokens.LastOrDefault(t => t.Category == LineTokenCategory.StepText || t.Category == LineTokenCategory.Variable);

                    if (firstInsertToken is object)
                    {
                        startInsertPos = firstInsertToken.Start(request.Position.Line);
                    }

                    if (lastInsertToken is object)
                    {
                        endInsertPos = lastInsertToken.End(request.Position.Line);
                    }

                    // Expand the set of possible matches so we get one entry for
                    // each possible placeholder value in the given step definition.
                    var expanded = ExpandPlaceholders(possibleMatches);

                    completionList = new CompletionList(
                        ExpandPlaceholders(possibleMatches).Select(m => new CompletionItem
                        {
                            // Label displayed to the user in the list.
                            Label = GetCompletionString(m, stepRef, CompletionStringMode.Label, out var _),
                            Kind = CompletionItemKind.Snippet,
                            Documentation = GetDocBlock(m.Match.Definition),

                            // Text used by VS Code to do in-memory filtering.
                            FilterText = GetCompletionString(m, stepRef, CompletionStringMode.Filter, out var _),
                            TextEdit = new TextEdit
                            {
                                // The insert text (in snippet format, with tab stops.)
                                NewText = GetCompletionString(m, stepRef, CompletionStringMode.Snippet, out var fmt),
                                Range = new Range(startInsertPos, endInsertPos),
                            },
                            InsertTextFormat = fmt,

                            // Auto-select this option if it's an exact match.
                            Preselect = m.Match.IsExact,
                        }), false);

                    return completionList;
                }
                else if ((position.CurrentScope is ScenarioElement || position.CurrentScope is StepDefinitionElement) && position.LineTokens.Count == 0)
                {
                    // In a scenario or a step def; no other tokens on the line. Start a step reference.
                    completionList = new CompletionList(
                        new[]
                        {
                            new CompletionItem { Label = "Given ", Kind = CompletionItemKind.Keyword },
                            new CompletionItem { Label = "When ", Kind = CompletionItemKind.Keyword },
                            new CompletionItem { Label = "Then ", Kind = CompletionItemKind.Keyword },
                            new CompletionItem { Label = "And ", Kind = CompletionItemKind.Keyword },
                        }, true);
                }
            }

            return completionList;
        }

        private StringOrMarkupContent? GetDocBlock(StepDefinition definition)
        {
            var docs = definition.GetDocumentation();

            if (docs is object)
            {
                return new StringOrMarkupContent(new MarkupContent { Value = docs, Kind = MarkupKind.Markdown });
            }

            return null;
        }

        private IEnumerable<ExpandedMatch> ExpandPlaceholders(IEnumerable<IMatchResult> matches)
        {
            foreach (var match in matches)
            {
                if (match.Definition.Definition is InteractionStepDefinitionElement interactionDef && interactionDef.ValidComponents.Any())
                {
                    // There are some valid components at work.
                    // Expand them into individual matches.
                    // Do we have a component placeholder in the match set?
                    if (match.PlaceholderValues is object && match.PlaceholderValues.TryGetValue(StepPlaceholders.Component, out var placeholderValue))
                    {
                        // Just the one match (with the component).
                        yield return new ExpandedMatch(match, placeholderValue);
                    }
                    else
                    {
                        // Expand the match.
                        foreach (var validComponent in interactionDef.ValidComponents)
                        {
                            yield return new ExpandedMatch(match, validComponent);
                        }
                    }
                }
                else
                {
                    yield return new ExpandedMatch(match);
                }
            }
        }

        private struct ExpandedMatch
        {
            public ExpandedMatch(IMatchResult match, string? componentName = null)
            {
                Match = match;
                ComponentName = componentName;
            }

            public IMatchResult Match { get; }

            public string? ComponentName { get; }
        }

        private enum CompletionStringMode
        {
            Label,
            Snippet,
            Filter,
        }

        private string GetCompletionString(ExpandedMatch match, StepReferenceElement stepRef, CompletionStringMode mode, out InsertTextFormat format)
        {
            // Work out the total length.
            var snippetLength = 0;

            const int placeholderBaseCharacterCount = 4;
            const string placeholderPrefix = "${";
            const string placeholderSeparator = ":";
            const string placeholderTerminator = "}";
            string newLine = Environment.NewLine;

            int argNumber = 0;

            var definition = match.Match.Definition.Definition;

            if (definition is null)
            {
                format = InsertTextFormat.PlainText;
                return string.Empty;
            }

            if (definition.Arguments.Count == 0 && !(definition is InteractionStepDefinitionElement intEl && intEl.ValidComponents.Any()))
            {
                format = InsertTextFormat.PlainText;
                return definition.Declaration ?? string.Empty;
            }

            var parts = definition.Parts;

            var startPartIdx = 0;

            format = InsertTextFormat.PlainText;

            // Each argument adds the extra characters necessary to create placeholders.
            for (int idx = startPartIdx; idx < parts.Count; idx++)
            {
                var part = parts[idx];

                if (part is ArgumentPart arg)
                {
                    // Do we know the value yet?
                    var knownArgValue = match.Match.Arguments.FirstOrDefault(a => a.ArgumentName == arg.Name);

                    if (knownArgValue is object)
                    {
                        snippetLength += knownArgValue.GetRawLength();

                        if (knownArgValue.StartExclusive)
                        {
                            snippetLength += 1;
                        }

                        if (knownArgValue.EndExclusive)
                        {
                            snippetLength += 1;
                        }
                    }
                    else if (mode == CompletionStringMode.Snippet)
                    {
                        argNumber++;
                        snippetLength += placeholderBaseCharacterCount + argNumber.ToString(CultureInfo.CurrentCulture).Length + arg.Name.Length;
                    }
                    else
                    {
                        snippetLength += part.Text.Length;
                    }

                    format = InsertTextFormat.Snippet;
                }
                else if (part is PlaceholderMatchPart placeholder && placeholder.PlaceholderValueName == StepPlaceholders.Component)
                {
                    if (match.ComponentName is object)
                    {
                        snippetLength += match.ComponentName.Length;
                    }
                    else
                    {
                        snippetLength += part.Text.Length;
                    }
                }
                else
                {
                    // Include the space.
                    snippetLength += part.Text.Length;
                }

                if (idx < parts.Count - 1)
                {
                    snippetLength++;
                }
            }

            snippetLength += newLine.Length;

            var createdStr = string.Create(snippetLength, (parts, match.ComponentName, stepRef, mode, startPartIdx), (span, m) =>
            {
                var partSet = m.parts;
                int argNumber = 0;

                for (int idx = m.startPartIdx; idx < partSet.Count; idx++)
                {
                    var part = partSet[idx];

                    if (part is ArgumentPart arg)
                    {
                        // Do we know the value yet?
                        var knownArgValue = match.Match.Arguments.FirstOrDefault(a => a.ArgumentName == arg.Name);

                        if (knownArgValue is object)
                        {
                            if (knownArgValue.StartExclusive)
                            {
                                span[0] = '\'';
                                span = span.Slice(1);
                            }

                            var raw = knownArgValue.GetRawText(m.stepRef.RawText ?? string.Empty);
                            raw.AsSpan().CopyTo(span);
                            span = span.Slice(raw.Length);

                            if (knownArgValue.EndExclusive)
                            {
                                span[0] = '\'';
                                span = span.Slice(1);
                            }
                        }
                        else if (m.mode == CompletionStringMode.Snippet)
                        {
                            argNumber++;
                            var argNumberStr = argNumber.ToString(CultureInfo.CurrentCulture);

                            placeholderPrefix.AsSpan().CopyTo(span);
                            span = span.Slice(placeholderPrefix.Length);

                            argNumberStr.AsSpan().CopyTo(span);
                            span = span.Slice(argNumberStr.Length);

                            placeholderSeparator.AsSpan().CopyTo(span);
                            span = span.Slice(placeholderSeparator.Length);

                            arg.Name.AsSpan().CopyTo(span);
                            span = span.Slice(arg.Name.Length);

                            placeholderTerminator.AsSpan().CopyTo(span);
                            span = span.Slice(placeholderTerminator.Length);
                        }
                        else
                        {
                            part.Text.AsSpan().CopyTo(span);
                            span = span.Slice(part.Text.Length);
                        }
                    }
                    else if (part is PlaceholderMatchPart placeholder && placeholder.PlaceholderValueName == StepPlaceholders.Component)
                    {
                        var compSpan = m.ComponentName.AsSpan();
                        compSpan.CopyTo(span);
                        span = span.Slice(compSpan.Length);
                    }
                    else
                    {
                        part.Text.AsSpan().CopyTo(span);
                        span = span.Slice(part.Text.Length);
                    }

                    if (idx < partSet.Count - 1)
                    {
                        span[0] = ' ';
                        span = span.Slice(1);
                    }
                }

                newLine.AsSpan().CopyTo(span);
            });

            return createdStr;
        }

        /// <inheritdoc/>
        public void SetCapability(CompletionCapability capability)
        {
            clientCapability = capability;
        }
    }
}
