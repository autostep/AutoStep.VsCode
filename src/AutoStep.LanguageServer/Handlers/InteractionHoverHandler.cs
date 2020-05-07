using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoStep.Definitions;
using AutoStep.Definitions.Interaction;
using AutoStep.Elements;
using AutoStep.Elements.Interaction;
using AutoStep.Elements.Test;
using AutoStep.Execution;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Handles requests for hover data in test files.
    /// </summary>
    public class InteractionHoverHandler : InteractionHandler, IHoverHandler
    {
        private bool supportsMarkdown;

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractionHoverHandler"/> class.
        /// </summary>
        /// <param name="workspace">The workspace host.</param>
        public InteractionHoverHandler(IWorkspaceHost workspace)
            : base(workspace)
        {
        }

        /// <inheritdoc/>
        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions { DocumentSelector = DocumentSelector };
        }

        /// <inheritdoc/>
        public async Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var position = await GetPositionInfoAsync(request.TextDocument, request.Position, cancellationToken);

            if (position.HasValue)
            {
                var pos = position.Value;

                if (pos.CurrentScope is MethodCallElement methodCall)
                {
                    return GetMethodCallHoverData(methodCall, pos.Scopes);
                }
            }

            return null;
        }

        private Hover? GetMethodCallHoverData(MethodCallElement methodCall, IReadOnlyList<BuiltElement> scopes)
        {
            var containingScope = scopes.OfType<InteractionDefinitionElement>().FirstOrDefault();

            if (containingScope is object)
            {
                var methodDef = Workspace.GetMethodDefinition(methodCall, containingScope);

                if (methodDef is object)
                {
                    string? documentation = GetMethodDocumentation(methodDef);

                    var signatureContent = new MarkedString(GetSignature(methodDef));

                    MarkedStringsOrMarkupContent content;

                    if (string.IsNullOrEmpty(documentation))
                    {
                        content = new MarkedStringsOrMarkupContent(signatureContent);
                    }
                    else
                    {
                        content = new MarkedStringsOrMarkupContent(signatureContent, documentation);
                    }

                    return new Hover
                    {
                        Contents = content,
                        Range = methodCall.Range(),
                    };
                }
            }

            return null;
        }

        private string GetSignature(InteractionMethod method)
        {
            var builder = new StringBuilder();

            if (supportsMarkdown)
            {
                builder.AppendLine("```");
            }

            builder.Append(method.Name);

            builder.Append('(');

            if (method is FileDefinedInteractionMethod fileMethod)
            {
                for (int idx = 0; idx < fileMethod.MethodDefinition.Arguments.Count; idx++)
                {
                    var arg = fileMethod.MethodDefinition.Arguments[idx];

                    if (idx > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(arg.Name);
                }
            }
            else
            {
                for (int idx = 0; idx < method.ArgumentCount; idx++)
                {
                    if (idx > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append("arg");
                    builder.Append(idx + 1);
                }
            }

            builder.Append(')');

            if (supportsMarkdown)
            {
                builder.AppendLine();
                builder.Append("```");
            }

            return builder.ToString();
        }

        /// <inheritdoc/>
        public void SetCapability(HoverCapability capability)
        {
            if (capability is null)
            {
                throw new ArgumentNullException(nameof(capability));
            }

            this.supportsMarkdown = capability.ContentFormat.Contains(MarkupKind.Markdown);
        }
    }
}
