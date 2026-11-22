using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Aurora {
    internal static partial class CloudConnection {
        internal static string OneDriveAuthorizeArguments {
            get {
                var options = new Dictionary<string, string> { { "access_scopes", MicrosoftScopes } };
                return AuthorizeArguments("onedrive", options);
            }
        }

        static string AuthorizeArguments(string provider, Dictionary<string, string> options) {
            string json = new JavaScriptSerializer().Serialize(options);
            string encoded = Convert.ToBase64String(Utf8.GetBytes(json)).TrimEnd('=');
            return "authorize " + provider + " " + Args.Q(encoded);
        }

        internal static string GoogleAuthorizeArguments(string seed) {
            var options = new Dictionary<string, string>();
            foreach (string line in seed.Split('\n')) {
                int separator = line.IndexOf('=');
                if (separator < 1) continue;
                string key = line.Substring(0, separator).Trim();
                if (key == "scope" || key == "client_id" || key == "client_secret")
                    options[key] = line.Substring(separator + 1).Trim();
            }
            return AuthorizeArguments("drive", options);
        }

        static void ConnectGoogle(Engine engine, string clearConfig, string seed, CancellationToken token) {
            var authorization = ReadCommand(engine, clearConfig, GoogleAuthorizeArguments(seed), 10, token, ReadQuotaText);
            if (authorization.Item1 != 0)
                throw new IOException(L.T("A autorização do Google não foi concluída. Tente conectar novamente.",
                    "Google authorization did not complete. Try connecting again."));
            var credentials = AuthorizationCredentials(authorization.Item2);
            string json = new JavaScriptSerializer().Serialize(credentials);
            token.ThrowIfCancellationRequested();
            File.WriteAllText(clearConfig, seed + "token = " + json + "\n", Utf8);
        }

        static void ConnectOneDrive(Engine engine, string clearConfig, CancellationToken token, Action<string> progress) {
            var authorization = ReadCommand(engine, clearConfig, OneDriveAuthorizeArguments, 10, token, ReadQuotaText);
            if (authorization.Item1 != 0)
                throw new IOException(L.T("A autorização do OneDrive não foi concluída. Tente conectar novamente.",
                    "OneDrive authorization did not complete. Try connecting again."));
            progress(L.T("Localizando seu OneDrive principal…", "Finding your primary OneDrive…"));
            string config = CompleteOneDriveAuthorization(authorization.Item2,
                (path, accessToken) => ReadOneDrive(path, accessToken, token));
            token.ThrowIfCancellationRequested();
            File.WriteAllText(clearConfig, config, Utf8);
        }

        static IDictionary<string, object> AuthorizationCredentials(string output) {
            var payload = Regex.Match(output ?? "", @"--->\s*([A-Za-z0-9+/]+)\s*<---End paste");
            if (!payload.Success)
                throw new InvalidDataException(L.T("O serviço não devolveu uma autorização válida.", "The service did not return valid authorization."));
            IDictionary<string, object> values;
            try {
                string encoded = payload.Groups[1].Value;
                encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
                values = Json(Utf8.GetString(Convert.FromBase64String(encoded)));
            } catch (FormatException) {
                throw new InvalidDataException(L.T("A resposta de autorização está incompleta.", "The authorization response is incomplete."));
            } catch (ArgumentException) {
                throw new InvalidDataException(L.T("A resposta de autorização está incompleta.", "The authorization response is incomplete."));
            }
            string tokenJson = StringValue(values, "token");
            IDictionary<string, object> credentials;
            try { credentials = Json(tokenJson); }
            catch (ArgumentException) { credentials = null; }
            catch (InvalidOperationException) { credentials = null; }
            string accessToken = StringValue(credentials, "access_token");
            if (String.IsNullOrWhiteSpace(accessToken) || accessToken.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new InvalidDataException(L.T("A autorização não contém uma sessão válida.", "Authorization does not contain a valid session."));
            return credentials;
        }

        internal static string CompleteOneDriveAuthorization(string output, Func<string, string, string> get) {
            var credentials = AuthorizationCredentials(output);
            string accessToken = StringValue(credentials, "access_token");
            // /me/drive identifies the signed-in user's default drive. /me/drives
            // can also expose internal libraries that reject root requests.
            var drive = Json(get("/me/drive?$select=id,driveType", accessToken));
            string driveId = StringValue(drive, "id");
            string driveType = StringValue(drive, "driveType");
            if (!Regex.IsMatch(driveId, @"\A[A-Za-z0-9_!-]{1,256}\z") ||
                (driveType != "personal" && driveType != "business"))
                throw new InvalidDataException(L.T("A Microsoft não informou um OneDrive principal compatível para esta conta.",
                    "Microsoft did not return a supported primary OneDrive for this account."));
            var root = Json(get("/drives/" + Uri.EscapeDataString(driveId) + "/root?$select=id", accessToken));
            if (String.IsNullOrWhiteSpace(StringValue(root, "id")))
                throw new InvalidDataException(L.T("Não foi possível confirmar a pasta principal do OneDrive.",
                    "Could not confirm the OneDrive root folder."));

            // Normalize the token before writing an INI value.
            string normalizedToken = new JavaScriptSerializer().Serialize(credentials);
            return OneDriveProfile + "drive_id = " + driveId + "\ndrive_type = " + driveType + "\ntoken = " + normalizedToken + "\n";
        }

        static string ReadOneDrive(string path, string accessToken, CancellationToken token) {
            var request = (HttpWebRequest)WebRequest.Create("https://graph.microsoft.com/v1.0" + path);
            request.AllowAutoRedirect = false;
            request.Timeout = request.ReadWriteTimeout = 20000;
            request.Headers[HttpRequestHeader.Authorization] = "Bearer " + accessToken;
            request.UserAgent = "OuO Cloud Analyzer/1.0";
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            try {
                using (token.Register(request.Abort))
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new IOException(L.T("A Microsoft não confirmou o acesso ao OneDrive. Abra o OneDrive no navegador e tente novamente.",
                            "Microsoft did not confirm OneDrive access. Open OneDrive in your browser and try again."));
                    token.ThrowIfCancellationRequested();
                    return ReadQuotaText(reader);
                }
            } catch (WebException error) {
                token.ThrowIfCancellationRequested();
                var response = error.Response as HttpWebResponse;
                string code = response == null ? "" : " (HTTP " + (int)response.StatusCode + ")";
                if (response != null) response.Dispose();
                throw new IOException(L.T("Não foi possível abrir seu OneDrive", "Could not open your OneDrive") + code +
                    L.T(". Abra o OneDrive no navegador, confirme que a conta funciona e tente conectar novamente.",
                        ". Open OneDrive in your browser, confirm the account works and try connecting again."));
            }
        }
    }
}
