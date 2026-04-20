using System.Diagnostics;
using System.Net;
using System.Text;
using FieldCure.Mcp.Outbox.Interaction;
using ModelContextProtocol.Protocol;

namespace FieldCure.Mcp.Outbox.OAuth;

/// <summary>
/// Drives a local browser-based OAuth authorization-code flow by opening the
/// user's browser, listening for the localhost callback, and optionally
/// coordinating with MCP elicitation so the user can confirm or cancel the
/// sign-in flow from the client UI.
/// </summary>
public sealed class BrowserOAuthFlow(int port = 9876)
{
    /// <summary>
    /// Default localhost port used for desktop OAuth callbacks.
    /// </summary>
    public const int DefaultPort = 9876;

    /// <summary>
    /// Default timeout for the interactive browser flow.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    readonly int _port = port;

    /// <summary>
    /// Gets the redirect URI that OAuth providers must call back to.
    /// </summary>
    public string RedirectUri => $"http://localhost:{_port}/callback";

    /// <summary>
    /// Returns a best-effort indication of whether the current host can support
    /// a local browser plus localhost callback OAuth flow.
    /// </summary>
    public static bool IsSupportedOnCurrentHost()
    {
        if (!HttpListener.IsSupported)
            return false;

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            return true;

        if (OperatingSystem.IsLinux())
        {
            // DISPLAY / WAYLAND_DISPLAY cover desktop Linux directly. WSL_DISTRO_NAME
            // catches WSL setups where xdg-open delegates to the Windows host
            // browser via the wslu package even when DISPLAY is unset.
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"));
        }

        return false;
    }

    /// <summary>
    /// Returns a concise explanation of the local-host requirements for browser OAuth.
    /// </summary>
    public static string GetUnsupportedReason() =>
        "This OAuth setup requires a local MCP server host with browser access and localhost callback support.";

    /// <summary>
    /// Runs the browser flow from an MCP tool by racing the callback listener
    /// against a user-facing confirmation prompt.
    /// </summary>
    /// <param name="gate">The elicitation gate used for follow-up confirmation.</param>
    /// <param name="authorizationUrl">The provider authorization URL to open.</param>
    /// <param name="providerName">User-facing provider name such as KakaoTalk or Microsoft.</param>
    /// <param name="ct">Cancellation token for the overall flow.</param>
    /// <returns>The callback outcome, including success, timeout, or cancellation.</returns>
    internal async Task<OAuthCodeResult> RunWithGateAsync(
        IElicitGate gate,
        string authorizationUrl,
        string providerName,
        CancellationToken ct = default)
    {
        using var listener = CreateListener();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DefaultTimeout);

        var browserOpened = TryOpenBrowser(authorizationUrl);
        var listenerTask = WaitForCallbackAsync(listener, timeoutCts.Token);
        var promptTask = PromptUserToContinueAsync(gate, providerName, authorizationUrl, browserOpened, timeoutCts.Token);
        var winner = await Task.WhenAny(listenerTask, promptTask);

        try
        {
            if (winner == listenerTask)
            {
                timeoutCts.Cancel();
                return await listenerTask;
            }

            if (!await promptTask)
            {
                timeoutCts.Cancel();
                return OAuthCodeResult.Cancelled("User cancelled the sign-in flow.");
            }

            using var graceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            graceCts.CancelAfter(TimeSpan.FromSeconds(15));
            return await listenerTask.WaitAsync(graceCts.Token);
        }
        catch (OperationCanceledException)
        {
            return OAuthCodeResult.Timeout();
        }
        finally
        {
            StopListener(listener);
        }
    }

    /// <summary>
    /// Runs the browser flow from a CLI command by opening the browser and
    /// waiting directly for the localhost callback.
    /// </summary>
    /// <param name="authorizationUrl">The provider authorization URL to open.</param>
    /// <param name="providerName">User-facing provider name such as KakaoTalk or Microsoft.</param>
    /// <param name="ct">Cancellation token for the overall flow.</param>
    /// <returns>The callback outcome, including success, timeout, or cancellation.</returns>
    public async Task<OAuthCodeResult> RunWithConsoleAsync(
        string authorizationUrl,
        string providerName,
        CancellationToken ct = default)
    {
        using var listener = CreateListener();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DefaultTimeout);

        var browserOpened = TryOpenBrowser(authorizationUrl);
        Console.WriteLine();
        Console.WriteLine(browserOpened
            ? $"Opening browser for {providerName} login..."
            : $"Open this URL in a browser on this machine to continue {providerName} sign-in:");

        if (!browserOpened)
            Console.WriteLine(authorizationUrl);

        Console.Write("Waiting for authorization... ");

        try
        {
            return await WaitForCallbackAsync(listener, timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            return OAuthCodeResult.Timeout();
        }
        finally
        {
            StopListener(listener);
        }
    }

    /// <summary>
    /// Creates and starts the localhost callback listener for the current redirect URI.
    /// </summary>
    /// <returns>An active <see cref="HttpListener"/> bound to the callback prefix.</returns>
    HttpListener CreateListener()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"{RedirectUri}/");
        listener.Start();
        return listener;
    }

    /// <summary>
    /// Stops the callback listener without surfacing shutdown exceptions.
    /// </summary>
    /// <param name="listener">The listener to stop.</param>
    static void StopListener(HttpListener listener)
    {
        try
        {
            if (listener.IsListening)
                listener.Stop();
        }
        catch
        {
        }
    }

    /// <summary>
    /// Opens the user's default browser on the local host as a best-effort action.
    /// </summary>
    /// <param name="url">The authorization URL to open.</param>
    /// <returns><see langword="true"/> when the browser launch request was accepted; otherwise <see langword="false"/>.</returns>
    static bool TryOpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Waits for the OAuth provider callback and writes a small HTML completion page
    /// back to the browser before returning the parsed result.
    /// </summary>
    /// <param name="listener">The active localhost callback listener.</param>
    /// <param name="ct">Cancellation token for the wait operation.</param>
    /// <returns>The parsed authorization-code callback outcome.</returns>
    static async Task<OAuthCodeResult> WaitForCallbackAsync(HttpListener listener, CancellationToken ct)
    {
        var context = await listener.GetContextAsync().WaitAsync(ct);
        var code = context.Request.QueryString["code"];
        var error = context.Request.QueryString["error"];
        var errorDescription = context.Request.QueryString["error_description"];

        var html = string.IsNullOrEmpty(error)
            ? "<html><body><h2>Authorization successful</h2><p>You can close this window.</p></body></html>"
            : $"<html><body><h2>Authorization failed</h2><p>{WebUtility.HtmlEncode(errorDescription ?? error)}</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = bytes.Length;
        // Write the response with an unlinked token so a late grace-window
        // cancellation cannot truncate the HTML page the user's browser sees.
        // The payload is small and completes near-instantly.
        await context.Response.OutputStream.WriteAsync(bytes, CancellationToken.None);
        context.Response.Close();

        if (!string.IsNullOrWhiteSpace(code))
            return OAuthCodeResult.Success(code);

        return OAuthCodeResult.Failure(error ?? "unknown", errorDescription ?? "Authorization did not return a code.");
    }

    /// <summary>
    /// Prompts the user through MCP to confirm that the browser-based sign-in
    /// has completed, while also surfacing the authorization URL as a fallback.
    /// </summary>
    /// <param name="gate">The elicitation gate for the active MCP request.</param>
    /// <param name="providerName">User-facing provider name such as KakaoTalk or Microsoft.</param>
    /// <param name="authorizationUrl">The authorization URL to display to the user.</param>
    /// <param name="browserOpened">Whether the browser launch request was accepted by the shell (best-effort).</param>
    /// <param name="ct">Cancellation token for the elicitation request.</param>
    /// <returns><see langword="true"/> when the user accepted the prompt; otherwise <see langword="false"/>.</returns>
    static async Task<bool> PromptUserToContinueAsync(
        IElicitGate gate,
        string providerName,
        string authorizationUrl,
        bool browserOpened,
        CancellationToken ct)
    {
        // We intentionally avoid claiming the browser "should already be open" —
        // Process.Start(UseShellExecute=true) returns success as soon as the
        // shell dispatches the URL, which does not guarantee a window actually
        // appeared. Always surface the URL so the user can open it themselves
        // if needed.
        var launchNote = browserOpened
            ? "A browser launch request was dispatched on the MCP server host."
            : "The browser did not launch automatically on the MCP server host.";
        var result = await gate.ElicitAsync(new ElicitRequestParams
        {
            Message =
                $"Complete sign-in for {providerName} in your browser.\n\n" +
                $"{launchNote} If no browser window opened, open this URL manually on that same machine:\n{authorizationUrl}\n\n" +
                "Accept this prompt once sign-in finishes, or decline to cancel.",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                {
                    ["continue"] = new ElicitRequestParams.BooleanSchema
                    {
                        Title = "Sign-in complete?",
                        Description = "Accept after the browser-based sign-in finishes, or decline to cancel.",
                    },
                },
                Required = ["continue"],
            },
        }, ct);

        return result.IsAccepted;
    }
}

/// <summary>
/// Represents the outcome of waiting for an OAuth authorization-code callback.
/// </summary>
/// <param name="IsSuccess">Whether the authorization code was received successfully.</param>
/// <param name="Code">The authorization code returned by the provider.</param>
/// <param name="Error">The provider-reported error code when the flow fails.</param>
/// <param name="ErrorDescription">A user-facing error description when available.</param>
/// <param name="IsCancelled">Whether the user explicitly cancelled the flow.</param>
/// <param name="IsTimeout">Whether the flow timed out before completion.</param>
public sealed record OAuthCodeResult(
    bool IsSuccess,
    string? Code,
    string? Error,
    string? ErrorDescription,
    bool IsCancelled,
    bool IsTimeout)
{
    /// <summary>
    /// Creates a successful OAuth callback result.
    /// </summary>
    public static OAuthCodeResult Success(string code) => new(true, code, null, null, false, false);

    /// <summary>
    /// Creates a provider error result.
    /// </summary>
    public static OAuthCodeResult Failure(string error, string? description) => new(false, null, error, description, false, false);

    /// <summary>
    /// Creates a user-cancelled result.
    /// </summary>
    public static OAuthCodeResult Cancelled(string reason) => new(false, null, "cancelled", reason, true, false);

    /// <summary>
    /// Creates a timeout result.
    /// </summary>
    public static OAuthCodeResult Timeout() => new(false, null, "timeout", "Sign-in did not complete in time.", false, true);
}
