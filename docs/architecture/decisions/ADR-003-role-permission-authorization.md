# ADR-003 — Role-Permission Authorization

## Status

Accepted

## Context

DentalPro requires individual accounts, role-based access and the ability for
an Administrator to configure which implemented application capabilities
are available to each role. Users may hold more than one role. Authorization
must reflect current assignments immediately, and clinical access must not
be granted to Administrator merely because of that role name.

## Decision

- ASP.NET Core Identity manages users and the fixed initial roles:
  Administrator, Dentist and Receptionist.
- Permissions are assigned to roles only. There are no user-specific
  permissions.
- A user's effective permissions are the union of permissions assigned to
  all of the user's current roles.
- The permission catalog has one canonical definition in backend code.
  Permission codes are stable authorization identifiers; display names and
  descriptions are mutable metadata.
- Only permissions for implemented endpoints are included. Initial patient
  permissions include `patients.view`, `patients.create` and `patients.update`;
  each is added only with its corresponding endpoint.
- Current role-permission assignments are checked by the API for every
  authorization decision. The MVP does not cache effective permissions, so
  assignment changes apply on the next request without a new login.
- The API requires authentication by default. Anonymous access is allowed
  only on explicitly designated endpoints such as login.
- Protected administrative permissions are defined in backend code and
  cannot be removed from Administrator through normal API operations.
- The last active Administrator cannot lose the Administrator role or be
  deactivated. The invariant must also hold for concurrent changes.
- Changing a user's roles or active state invalidates previously issued JWTs
  through per-request validation of current security state. Failure to
  validate that state rejects the request.
- Initial roles, permission catalog entries and protected assignments are
  initialized idempotently. Initialization must not silently delete
  existing role-permission assignments. The first Administrator is created
  through a secure operational bootstrap, not a public endpoint.

The initial role-permission matrix and complete initial catalog are
maintained in `docs/domain/domain-model.md`.

## Consequences

### Positive

- Administrators can adjust access to implemented capabilities without a
  code deployment.
- Permission changes do not wait for an access token to be renewed.
- The backend remains the authority for access decisions.
- The dental domain remains independent of Identity and persistence
  technology.

### Negative

- Authorization requires database-backed lookups of security state and
  current role-permission assignments.
- Administratively configurable access requires protection of foundational
  permissions and the last active Administrator invariant.
- Retiring catalog codes requires an explicit data strategy; initialization
  must not silently delete assignments.

## Alternatives considered

### Permissions embedded in JWT as the authorization source

Rejected because permission changes would remain stale until a new token was
issued or would require token revocation for every permission assignment
change.

### Cached effective permissions in the MVP

Deferred because a cache would require reliable invalidation across all API
instances to meet the next-request effect requirement.

### Permissions assigned directly to users

Rejected because access is managed exclusively through role-permission
assignments.
