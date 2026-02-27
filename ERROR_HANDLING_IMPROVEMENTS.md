# CITE API - Error Handling Improvements

## Branch: `feature/improve-error-handling`

## Overview

This branch improves error handling and resilience in the CITE API, particularly for operations called by Blueprint during MSEL deployment (push/pull integrations).

## Changes Made

### 1. Enhanced TeamService Error Handling

**Location**: `Cite.Api/Services/TeamService.cs`

**Improvements**:
- **Input Validation**: Validates required fields (Name, EvaluationId) before attempting database operations
- **Foreign Key Validation**: Pre-validates that Evaluation and TeamType exist before creating team
- **Database Exception Handling**: Catches and translates DbUpdateException into user-friendly error messages
- **PostgreSQL-specific Error Codes**: Handles PostgreSQL constraint violations:
  - `23505`: Unique constraint violation (duplicate team name)
  - `23503`: Foreign key violation (invalid Evaluation or TeamType reference)
  - `23514`: Check constraint violation (data validation failure)
- **SQL Server Support**: Also handles SQL Server error codes for compatibility
- **Timeout Handling**: Catches and logs timeout exceptions with clear error messages
- **Structured Logging**: Adds detailed logging at INFO level for operations and ERROR level for failures
- **Cancellation Support**: Properly handles OperationCanceledException

**Error Messages**:
- Clear, actionable error messages instead of raw database errors
- Example: "TeamType {id} not found" instead of "23503: foreign key violation"

### 2. Enhanced TeamMembershipService Error Handling

**Location**: `Cite.Api/Services/TeamMembershipService.cs`

**Improvements**:
- **Input Validation**: Validates required fields (TeamId, UserId)
- **Existence Checks**: Validates that Team and User exist before creating membership
- **Duplicate Detection**: Checks for existing membership before attempting insert
- **Database Exception Handling**: Similar comprehensive error handling as TeamService
- **Structured Logging**: Detailed logging of all operations and failures
- **Added Logger**: Injected ILogger<ITeamMembershipService> for proper logging

**Benefits**:
- Prevents silent failures when adding users to teams
- Provides clear error messages when teams or users don't exist
- Logs detailed information for debugging deployment issues

## Problems Solved

### Before

1. **Silent Failures**: Operations would fail with generic database errors
2. **Cryptic Error Messages**: Users saw raw PostgreSQL error codes (e.g., "23503")
3. **No Pre-validation**: Invalid foreign keys discovered only during SaveChanges
4. **Minimal Logging**: Hard to diagnose deployment failures
5. **Timeout Hangs**: Operations would hang with no clear error message

### After

1. **Early Validation**: Invalid data caught before database operations
2. **Clear Error Messages**: User-friendly messages like "TeamType {id} not found"
3. **Comprehensive Logging**: Detailed logs at INFO level for success, ERROR for failures
4. **Better Timeout Handling**: Timeouts caught and logged with context
5. **Proper Exception Types**: InvalidOperationException with clear messages instead of DbUpdateException

## Real-World Example

**Before**:
```
Blueprint pushes MSEL to CITE
→ Team creation fails with "23503: foreign_key_violation"
→ Blueprint hangs on "Pushing Teams to CITE"
→ No clear error in logs
→ Only 1 of 7 teams created
```

**After**:
```
Blueprint pushes MSEL to CITE
→ Team validation fails immediately
→ Error: "TeamType fd393dcb-2b46-4855-8144-bb991172c361 not found"
→ Logged: "Team creation failed: TeamType {id} not found"
→ Blueprint receives clear error message
→ Can display in deployment progress dialog
```

## Testing

### Manual Test Steps

1. **Test Valid Team Creation**:
   ```bash
   POST /api/teams
   {
     "name": "Test Team",
     "evaluationId": "{valid-eval-id}",
     "teamTypeId": "{valid-type-id}"
   }
   ```
   - Verify team is created
   - Check logs for INFO message: "Creating team Test Team..."
   - Check logs for success message: "Team Test Team ... created by ..."

2. **Test Invalid Evaluation**:
   ```bash
   POST /api/teams
   {
     "name": "Test Team",
     "evaluationId": "00000000-0000-0000-0000-000000000000"
   }
   ```
   - Verify 404 response with message: "Evaluation {id} not found"
   - Check logs for ERROR message

3. **Test Invalid TeamType**:
   ```bash
   POST /api/teams
   {
     "name": "Test Team",
     "evaluationId": "{valid-eval-id}",
     "teamTypeId": "00000000-0000-0000-0000-000000000000"
   }
   ```
   - Verify 404 response with message: "TeamType {id} not found"
   - Check logs for ERROR message

4. **Test Duplicate Team Name**:
   - Create team with name "Singapore"
   - Try to create another team with same name in same evaluation
   - Verify error: "A team with this name already exists in the evaluation"

5. **Test Team Membership with Invalid Team**:
   ```bash
   POST /api/teammemberships
   {
     "teamId": "00000000-0000-0000-0000-000000000000",
     "userId": "{valid-user-id}"
   }
   ```
   - Verify 404 response: "Team {id} not found"

6. **Test Duplicate Membership**:
   - Add user to team
   - Try to add same user again
   - Verify error: "User is already a member of this team"

## Database Compatibility

The error handling code supports both:
- **PostgreSQL** (primary): Uses Npgsql exception handling
- **SQL Server**: Uses SqlException handling for compatibility

Both database types will receive appropriate error messages.

## Logging Improvements

### New Log Levels

- **LogInformation**: Normal operations (team creation, membership addition)
- **LogWarning**: Successful operations that might need auditing (team created/updated/deleted)
- **LogError**: Failures with full exception details and context

### Log Examples

```
INFO: Creating team Singapore (abc-123) in Evaluation def-456 with TeamType ghi-789
WARNING: Team Singapore (abc-123) in Evaluation def-456 created by user-001
ERROR: Team creation failed: TeamType fd393dcb-2b46-4855-8144-bb991172c361 not found
ERROR: Database error creating team Singapore in Evaluation def-456: [exception details]
ERROR: Timeout creating team Singapore in Evaluation def-456
```

## Integration with Blueprint

These improvements will benefit Blueprint's integration push/pull operations:

1. **Clearer Errors**: Blueprint can display specific error messages in deployment progress dialog
2. **Better Logging**: Easier to diagnose why deployments fail
3. **Early Validation**: Catches invalid data before starting lengthy operations
4. **No Silent Failures**: All errors are logged and returned to caller

## Future Enhancements

1. **Retry Logic**: Add automatic retry for transient failures (timeouts, deadlocks)
2. **Bulk Operations**: Add batch team/membership creation with proper error handling
3. **Validation Service**: Extract validation logic into separate service
4. **Health Checks**: Add health check endpoints for database connectivity
5. **Circuit Breaker**: Implement circuit breaker pattern for external dependencies

## Breaking Changes

**None** - These changes are backward compatible:
- API contracts unchanged
- Error responses improved but still use standard HTTP status codes
- Existing clients will receive better error messages

## Performance Impact

**Minimal** - Added validation queries are lightweight:
- `EvaluationId` validation: Single `AnyAsync` query with index lookup
- `TeamTypeId` validation: Single `AnyAsync` query with index lookup
- `User` validation: Single `AnyAsync` query with index lookup
- All validations use indexes and are very fast

The performance cost is negligible compared to the benefits of early error detection.

## Compatibility

- Compatible with existing CITE UI
- Compatible with Blueprint API integration calls
- Compatible with any other API clients
- Works with both PostgreSQL and SQL Server databases

## Installation

1. Merge this branch into main
2. No database migrations required
3. No configuration changes required
4. Restart CITE API service

## Related Issues

- Fixes Blueprint hanging on "Pushing Teams to CITE" with unclear errors
- Fixes "23503: foreign_key_violation" cryptic error messages
- Improves logging for debugging deployment issues
- Makes error messages actionable for administrators

## References

- Blueprint integration code: `/mnt/data/crucible/blueprint/blueprint.api/Blueprint.Api/Infrastructure/Extensions/IntegrationCiteExtensions.cs`
- Blueprint deployment improvements: `/mnt/data/crucible/blueprint/blueprint.ui/DEPLOYMENT_IMPROVEMENTS.md`
- Documentation: `/workspaces/crucible-development/docs/blueprint-import-validation-improvements.md`
