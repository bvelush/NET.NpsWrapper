# TODOs and bugs

- [x] Init logging in AuthClient reports "Auth"
- [x] Local NoMFA groups are not resolved correctly
- [?] Static linking of DLLs (https://stackoverflow.com/questions/1868449/static-linking-of-libraries-created-on-c-sharp-net)
- [x] Consider refactoring for testability (HttpClient injection)
  - [x] Refactor Authenticator to accept optional HttpClient parameter
  - [x] Add _ownsHttpClient flag to prevent disposing injected instances
  - [x] Create comprehensive mocked HTTP tests
  - [x] Test immediate success/failure responses
  - [x] Test polling scenarios (pending -> success/failure)
  - [x] Test error handling (HTTP errors, invalid JSON, etc.)
  - [x] Verify request body content