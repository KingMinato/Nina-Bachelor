using System.Collections.Generic;
using UnityEngine;

// Backend for the result backup. Kept behind an interface so the Google Sheet can be swapped
// if the university requires a different storage.
public interface IResultSubmitter
{
    // Completes with true on success. Must never throw and must finish within the timeout.
    Awaitable<bool> Submit(IReadOnlyList<SessionData.Field> fields, float timeoutSeconds);
}
