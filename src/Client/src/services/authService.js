import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// Token endpoints answer { accessToken, expiresAt, user }; the refresh token travels only as an HttpOnly cookie.
export const authService = {
  login: (email, password) => axiosClient.post(ENDPOINTS.auth.token, { email, password }).then((r) => r.data),
  refresh: () => axiosClient.post(ENDPOINTS.auth.refresh).then((r) => r.data),
  revoke: () => axiosClient.post(ENDPOINTS.auth.revoke),
  changePassword: (body) => axiosClient.post(ENDPOINTS.auth.changePassword, body).then((r) => r.data),
  getProfile: () => axiosClient.get(ENDPOINTS.profile).then((r) => r.data),
  updateProfile: (body) => axiosClient.put(ENDPOINTS.profile, body).then((r) => r.data),
};
