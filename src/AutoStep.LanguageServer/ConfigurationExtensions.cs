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
        private static readonly string[] DefaultTestGlobs = new[] { "**/*.as" };
        private static readonly string[] DefaultInteractionGlobs = new[] { "**/*.asi" };

        /// <summary>
        /// Get the set of globs for test files.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <returns>A glob set.</returns>
        public static string[] GetTestFileGlobs(this IConfiguration config)
        {
            var globSection = config.GetSection("tests");

            if (globSection.Exists())
            {
                return globSection.Get<string[]>();
            }
            else
            {
                return DefaultTestGlobs;
            }
        }

        /// <summary>
        /// Get the set of globs for interaction files.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <returns>A glob set.</returns>
        public static string[] GetInteractionFileGlobs(this IConfiguration config)
        {
            var globSection = config.GetSection("interactions");

            if (globSection.Exists())
            {
                return globSection.Get<string[]>();
            }
            else
            {
                return DefaultInteractionGlobs;
            }
        }

        /// <summary>
        /// Gets the configuration for package extensions from the 'extensions' config element.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <returns>A set of package configs.</returns>
        public static PackageExtensionConfiguration[] GetPackageExtensionConfiguration(this IConfiguration config)
        {
            var all = config.GetSection("extensions").Get<PackageExtensionConfiguration[]>() ?? Array.Empty<PackageExtensionConfiguration>();

            if (all.Any(p => string.IsNullOrWhiteSpace(p.Package)))
            {
                throw new ProjectConfigurationException(ConfigurationMessages.PackageIdRequired);
            }

            return all;
        }

        /// <summary>
        /// Gets the configuration for local folder extensions from the 'localExtensions' config element.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <returns>A set of package configs.</returns>
        public static FolderExtensionConfiguration[] GetLocalExtensionConfiguration(this IConfiguration config)
        {
            var all = config.GetSection("localExtensions").Get<FolderExtensionConfiguration[]>() ?? Array.Empty<FolderExtensionConfiguration>();

            if (all.Any(p => string.IsNullOrWhiteSpace(p.Folder)))
            {
                throw new ProjectConfigurationException(ConfigurationMessages.FolderRequired);
            }

            return all;
        }
    }
}
