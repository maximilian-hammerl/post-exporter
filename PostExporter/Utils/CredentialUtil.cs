using System.Windows.Controls;
using Meziantou.Framework.Win32;

namespace PostExporter.Utils;

public static class CredentialUtil
{
    public static void UpdateCredential(string username, string password)
    {
        CredentialManager.WriteCredential(
            applicationName: Util.GetAppName(),
            userName: username,
            secret: password,
            persistence: CredentialPersistence.LocalMachine);
    }

    public static (string Username, string Password)? ReadCredential()
    {
        var credential = CredentialManager.ReadCredential(applicationName: Util.GetAppName());
        if (credential is null || credential.UserName is null || credential.Password is null)
        {
            return null;
        }
        return (Username: credential.UserName, credential.Password);
    }

    public static void DeleteCredential()
    {
        CredentialManager.DeleteCredential(applicationName: Util.GetAppName());
    }
}