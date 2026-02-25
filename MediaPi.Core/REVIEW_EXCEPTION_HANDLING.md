// REVIEW: Controllers Exception Handling - Database Constraints
// ============================================================
// 
// FINDINGS:
// ---------
// 
// 1. DevicesController.Menu.cs (UpdateConfiguration)
//    Location: Lines 27-40
//    Status: ✓ OK - Exception handling is for AGENT communication, not DB constraints
//    Details: Catches HttpClient timeouts from agent, not database errors
//             Can safely coexist with DatabaseConstraintMiddleware
//             
// 
// 2. DeviceMonitoringService.cs (ExecuteAsync SaveChangesAsync)
//    Location: Lines 203-234
//    Status: ⚠️ GENERIC EXCEPTION HANDLING
//    Current: catch (Exception ex) { _logger.LogError(ex, "Error saving..."); }
//    Issue: Swallows all database errors silently (includes DbUpdateException)
//    Impact: Silent failures on constraint violations, no user feedback
//    
//    RECOMMENDATION: Remove generic catch, let middleware handle
//                   Middleware will log and device probes will be skipped for that iteration
//
//
// 3. DeviceStatusesController.cs
//    Status: ✓ OK - No SaveChangesAsync, only reads data
//             Stream method has proper OperationCanceledException handling
//
// 
// 4. VideosController.cs
//    Status: ✓ OK - No explicit exception handling around SaveChangesAsync
//             Will benefit from DatabaseConstraintMiddleware for constraint violations
//
//
// 5. UsersController.cs
//    Status: ✓ OK - No explicit exception handling around SaveChangesAsync
//             Email existence checked BEFORE save attempt (validates proactively)
//             Will benefit from DatabaseConstraintMiddleware if constraints violated
//
//
// 6. DevicesController.cs
//    Status: ✓ OK - No SaveChangesAsync in main handler
//             Exception handling only for agent communications (502 errors)
//
//
// SUMMARY:
// --------
// Only DeviceMonitoringService has generic exception handling that should be cleaned up.
// All other controllers are fine - they either:
// - Don't catch DB exceptions
// - Don't call SaveChangesAsync in try-catch blocks
// - Catch specific errors (OperationCanceledException) not DB exceptions
// 
// The middleware will transparently handle all DbUpdateException across all controllers,
// providing consistent error responses (409 Conflict) for constraint violations.
