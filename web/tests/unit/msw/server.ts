import { setupServer } from 'msw/node';

/**
 * No unit test touches a real server. Handlers are added per test with
 * `server.use(...)`, so an unhandled request is a failure rather than a
 * silent pass.
 */
export const server = setupServer();
