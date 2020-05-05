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
