# Changelog

All notable changes to this fork of [Remotely](https://github.com/immense/Remotely)
are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

The version numbers are the build stamps (`yyyy.MM.dd.HHmm`) that
`Utilities/Publish.ps1` writes into the assemblies, and the same stamp is
written into the container image tag by the release workflow.

The release procedure and the release gates live in `RELEASE-CHECKLIST.md`;
the status of the gates lives in `GATE-REGISTER.md`.

## [Unreleased]

### Security

- The Content-Security-Policy allows the two Google Fonts origins, because
  the Bootswatch themes import their fonts from there.  Without the exception
  the browser blocked the stylesheets, the console lost its fonts, and every
  page logged a policy error.
- The buttons that had inline `onclick` handlers (the log downloads and the
  cancel button of the invitation page) work through the Blazor event model
  now.  The inline handlers were blocked by the policy, so the buttons did
  nothing.
- The relay of a control message is authorized against the viewer's own
  mode, and the session-wide flag no longer grants a viewer more than the
  server decided.
- `Ctrl+Alt+Del` and the Windows session switch are refused for a viewer
  that is in view-only mode.
- The "notify once" flag moved from a shared hub field to the connection
  context, so one viewer can no longer suppress the notification of another.
- A Content-Security-Policy and the baseline security headers are sent with
  every response. The policy can be turned off with
  `Remotely_ApplicationOptions__EnableContentSecurityPolicy=false`.
- An API key only writes to the database after a successful authentication.
- The API script result cache is bounded and registered in the container.
- The set of viewer connection ids is thread safe.

### Foundation

- Integration tests run against a real SQLite database: the migration chain
  applies cleanly and is idempotent, the device round trip does not create a
  duplicate row, and an unknown organization is rejected.
- The settings of the server were extracted into `ISettingsService`. The
  semaphore that serializes the settings row lived on a transient service
  and therefore serialized nothing; it is now a singleton with one gate.
- The devices were extracted into `IDeviceService`: registration, updates,
  the online state, and the media and keyboard settings of a device.
  `IDataService` inherits the narrow interfaces, so the existing callers keep
  working.
- The API tokens were extracted into `IApiTokenService`, and the alerts into
  `IAlertService`. The rule that decides who may see a device moved into a
  shared helper instead of being copied.
- The scripts were extracted into `ISavedScriptService`,
  `IScriptScheduleService`, and `IScriptResultService`; the shared files into
  `ISharedFileService`; the device listings into `IDeviceListingService`; the
  membership of the device groups into `IDeviceGroupMembershipService`; and
  the cleanup of the old records into `IMaintenanceService`.
  `IDataService` is now only a façade over the fifteen domain interfaces, and
  `DataService` only forwards to them.
- The call sites were migrated to the narrow interfaces: every controller,
  hub, filter, service, and component asks for the domain it uses. The god
  interface and its implementation were removed, so a component can no longer
  reach the whole data layer by accident.
- Saving a script now stores it when it is new and updates it when it
  already exists. The update alone marked a new script as modified, and the
  update of a row that was never stored ended in a concurrency exception.
- The organizations and their branding were extracted into
  `IOrganizationService`.
- The users were extracted into `IUserService`, the invitation links into
  `IInviteService`, and the device groups into `IDeviceGroupService`.
- Checking whether a user may see a device no longer dereferences a missing
  device group. The relational providers answered with a false, and the
  in-memory provider threw a null reference; both answer the same now.
- Resetting the branding of an organization threw an exception: it copied the
  whole default object, including its empty key, and EF refuses to change the
  key of a tracked entity. Only the values are copied now.
- An alert for a device that does not exist is refused with a warning
  instead of a foreign key error from the database. The alerts table has a
  foreign key to the devices, so the error only appeared on the relational
  providers.
- The application options are validated when the host starts: an unsupported
  database provider or a malformed Docker gateway address stops the process
  with a clear message instead of being discovered later.

### Platform

- The readiness probe answers with the status of the database and returns
  `503` when it cannot be reached. It stays restricted to the local machine.
- The server starts in a container without a volume: the data directory is
  created in the image, and a database that cannot be prepared produces an
  actionable message instead of a raw provider exception.
- The DataProtection keys are stored in the persistent data directory, so a
  restart does not sign every user out.
- The build context of the image is limited to the published server output.
- The CI gained a job that applies the migrations to disposable PostgreSQL
  and SQL Server databases, and a job that runs the image and probes it from
  inside the container.
- The agent is published in CI with the version stamp that the deployment
  requires, and the published output is kept as an artifact.
- Every request carries a correlation id that reaches the log file, and the
  counters of the process are exposed in the Prometheus text format.
- The server takes a lease on the database and reports when another instance
  is using it, because there is no SignalR backplane.
- Every action of the API states how it is protected, and a test fails when a
  new controller is added without that decision.
- The server verifies the manifest it signs with the same code the agents
  use, so a package that the agents would refuse never stays published.

### Documentation

- The generated `EGM.md` structural maps are no longer tracked. They are
  rebuilt from the source, and the status of the project lives in
  `GATE-REGISTER.md`, not in them.

### Known gaps

- The release gates are not passed: none of the checks in
  `RELEASE-CHECKLIST.md` has been executed in a deployment context.
- The CSP was not verified in a live browser.
- The update signing key is protected with DPAPI on Windows only.
- There is no SignalR backplane, so the server runs as a single instance.
