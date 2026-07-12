# 0003: Dev-only "current user" switcher instead of real auth

**What:** There is no authentication yet. The acting user is resolved by
`ICurrentUserAccessor` from an `X-Settl-User: {memberId}` request header, falling back to
the seeded "Du". The web app ships a dev-only user picker (gated on `import.meta.env.DEV`)
that sets this header. `GET /me` and `GET /dev/users` back it.

**Why we took it:** ADR-0005 fixes auth as self-hosted ASP.NET Identity but explicitly
defers the build (cookie-vs-JWT, invite flow deserve their own grill). The whole design
and every flow can be built and verified against a single abstraction seam without auth
in the way.

**Trigger to pay it down:** Building the auth feature (ADR-0005). At that point
`ICurrentUserAccessor` is reimplemented against the authenticated principal, the header
path and `GET /dev/users` are removed, and the web user picker is deleted. Nothing else
in the API should need to change — that is the point of the seam.
