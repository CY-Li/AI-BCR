using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Windows.System;

namespace PlustekBCR.Services
{
    public sealed class DefaultLinkAppService : IDefaultLinkAppService
    {
        public Task<DefaultLinkAppsSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new DefaultLinkAppsSnapshot(
                    QueryProtocol("tel"),
                    QueryProtocol("mailto"),
                    QueryProtocol("http"),
                    QueryProtocol("https"));
            }, cancellationToken);

        public async Task<bool> OpenWindowsDefaultAppsSettingsAsync()
        {
            try
            {
                return await Launcher.LaunchUriAsync(new Uri("ms-settings:defaultapps"));
            }
            catch
            {
                return false;
            }
        }

        private static DefaultLinkAppInfo QueryProtocol(string protocol)
        {
            try
            {
                var friendlyName = QueryAssociation(protocol, AssociationString.FriendlyApplicationName);
                if (!string.IsNullOrWhiteSpace(friendlyName))
                {
                    return new DefaultLinkAppInfo(protocol, DefaultLinkAppState.Available, friendlyName.Trim());
                }

                var executablePath = QueryAssociation(protocol, AssociationString.Executable);
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return new DefaultLinkAppInfo(protocol, DefaultLinkAppState.NotConfigured);
                }

                var displayName = GetExecutableDisplayName(executablePath);
                return new DefaultLinkAppInfo(protocol, DefaultLinkAppState.Available, displayName);
            }
            catch
            {
                return new DefaultLinkAppInfo(protocol, DefaultLinkAppState.Unavailable);
            }
        }

        private static string? QueryAssociation(string protocol, AssociationString associationString)
        {
            const AssociationFlags flags = AssociationFlags.IsProtocol | AssociationFlags.AppToApp;
            uint characterCount = 0;
            _ = AssocQueryString(flags, associationString, protocol, null, null, ref characterCount);

            if (characterCount == 0)
            {
                return null;
            }

            var value = new StringBuilder((int)characterCount);
            var result = AssocQueryString(flags, associationString, protocol, null, value, ref characterCount);
            return result == 0 ? value.ToString() : null;
        }

        private static string GetExecutableDisplayName(string executablePath)
        {
            try
            {
                var description = FileVersionInfo.GetVersionInfo(executablePath).FileDescription;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    return description.Trim();
                }
            }
            catch
            {
                // Fall back to the executable name when metadata cannot be read.
            }

            var fileName = Path.GetFileNameWithoutExtension(executablePath);
            return string.IsNullOrWhiteSpace(fileName) ? executablePath : fileName;
        }

        [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode, EntryPoint = "AssocQueryStringW")]
        private static extern int AssocQueryString(
            AssociationFlags flags,
            AssociationString associationString,
            string association,
            string? extra,
            StringBuilder? output,
            ref uint outputCharacterCount);

        [Flags]
        private enum AssociationFlags : uint
        {
            IsProtocol = 0x00001000,
            AppToApp = 0x00010000
        }

        private enum AssociationString
        {
            Executable = 2,
            FriendlyApplicationName = 4
        }
    }
}
