using System;
using System.Linq;
using AutoStep.Extensions;
using Microsoft.Extensions.Configuration;

namespace AutoStep.LanguageServer
{
    /// <summary>
    /// Extensions to the configuration file that help access common configuration properties.
    /// </summary>
    internal static class ConfigurationExtensions
    {
        /// <summary>
        /// Gets the set of test file globs.
        /// </summary>
        /// <param name="config">The project config.</param>
        /// <returns>A set of glob paths.</returns>
        public static string[] GetTestFileGlobs(this IConfiguration config)
        {
            return config.GetValue("tests", new[] { "**/*.as" });
        }

        /// <summary>
        /// Gets the set of interaction file globs.
        /// </summary>
        /// <param name="config">The project config.</param>
        /// <returns>A set of glob paths.</returns>
        public static string[] GetInteractionFileGlobs(this IConfiguration config)
        {
            return config.GetValue("interactions", new[] { "**/*.asi" });
        }

        public static PackageExtensionConfiguration[] GetPackageExtensionConfiguration(this IConfiguration config)
        {
            var all = config.GetSection("extensions").Get<PackageExtensionConfiguration[]>() ?? Array.Empty<PackageExtensionConfiguration>();

            if (all.Any(p => string.IsNullOrWhiteSpace(p.Package)))
            {
                throw new ProjectConfigurationException("Extensions must have a 'package' value containing the package ID.");
            }

            return all;
        }

        public static FolderExtensionConfiguration[] GetLocalExtensionConfiguration(this IConfiguration config)
        {
            var all = config.GetSection("localExtensions").Get<FolderExtensionConfiguration[]>() ?? Array.Empty<FolderExtensionConfiguration>();

            if (all.Any(p => string.IsNullOrWhiteSpace(p.Folder)))
            {
                throw new ProjectConfigurationException("Local Extensions must have a 'folder' value containing the name of the extension's folder.");
            }

            return all;
        }
    }
}
