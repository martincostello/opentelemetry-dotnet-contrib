// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AspNetCore.Implementation;

namespace OpenTelemetry.Instrumentation.AspNetCore;

/// <summary>
/// Asp.Net Core Requests instrumentation.
/// </summary>
internal sealed class AspNetCoreInstrumentation : IDisposable
{
    internal static readonly Version SemanticConventionsVersion = new(1, 42, 0);

    internal static readonly Func<string, object?, object?, bool> IsEnabled = static (eventName, _, _)
        => IsDiagnosticSourceEventEnabled(eventName);

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    public AspNetCoreInstrumentation(HttpInListener httpInListener)
    {
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(httpInListener, IsEnabled, AspNetCoreInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
        => this.diagnosticSourceSubscriber?.Dispose();

    /// <summary>
    /// Determines whether the specified ASP.NET Core diagnostic event is one the instrumentation handles.
    /// </summary>
    /// <param name="eventName">The name of the diagnostic event.</param>
    /// <returns>
    /// <see langword="true"/> if the event is handled by the instrumentation; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This is called for every <c>IsEnabled</c> check made against the <c>Microsoft.AspNetCore</c>
    /// <see cref="System.Diagnostics.DiagnosticListener"/> (hosting, routing, MVC filters, etc.), most of
    /// which are for events that are not handled, so it uses a compiler-generated string <see langword="switch"/>
    /// (ordinal and exact) which is cheaper than a hash-based lookup for this small set of event names.
    /// </remarks>
    internal static bool IsDiagnosticSourceEventEnabled(string eventName) => eventName switch
    {
        HttpInListener.ActivityOperationName or
        HttpInListener.OnStartEvent or
        HttpInListener.OnStopEvent or
        HttpInListener.OnUnHandledDiagnosticsExceptionEvent or
        HttpInListener.OnUnhandledHostingExceptionEvent => true,
        _ => false,
    };
}
