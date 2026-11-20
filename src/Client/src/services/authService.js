import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// Tabs of one browser share the refresh cookie: refreshing in two tabs at once would present the same token twice.
// A browser-wide lock makes them take turns, so the second tab refreshes with the cookie the first one received.
const withRefreshLock = (run) =>
  typeof navigator !== "undefined" && navigator.locks?.request ? navigator.locks.request("wpsm-token-refresh", run) : run();

// Token endpoints answer { accessToken, expiresAt, user }; the refresh token travels only as an HttpOnly cookie.
export const authService = {
  login: (email, password) => axiosClient.post(ENDPOINTS.auth.token, { email, password }).then((r) => r.data),
  refresh: () => withRefreshLock(() => axiosClient.post(ENDPOINTS.auth.refresh).then((r) => r.data)),
  revoke: () => axiosClient.post(ENDPOINTS.auth.revoke),
  changePassword: (body) => axiosClient.post(ENDPOINTS.auth.changePassword, body).then((r) => r.data),
  getProfile: () => axiosClient.get(ENDPOINTS.profile).then((r) => r.data),
  updateProfile: (body) => axiosClient.put(ENDPOINTS.profile, body).then((r) => r.data),
  // Signed-in devices of this account.
  listSessions: () => axiosClient.get(ENDPOINTS.auth.sessions).then((r) => r.data),
  revokeSession: (id) => axiosClient.delete(ENDPOINTS.auth.session(id)),
  revokeOtherSessions: () => axiosClient.post(ENDPOINTS.auth.revokeOtherSessions),
};
