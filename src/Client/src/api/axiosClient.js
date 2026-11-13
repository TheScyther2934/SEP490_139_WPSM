import axios from "axios";
import { ENDPOINTS } from "./endpoints";

// The store plugs its token getter and refresh action in at startup (see store/index.js),
// so this file does not import the store and no import cycle appears.
const auth = { getAccessToken: () => null, refresh: async () => null, expired: () => {} };

export function configureAuth(handlers) {
  Object.assign(auth, handlers);
}

const axiosClient = axios.create({
  baseURL: "/api/wpsm",
  headers: { Accept: "application/json" },
});

axiosClient.interceptors.request.use((config) => {
  const token = auth.getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// One refresh at a time: requests failing together wait for the same refresh, then retry once.
let refreshing = null;
const isTokenEndpoint = (url = "") => url.startsWith(ENDPOINTS.auth.token);

axiosClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config;
    if (error.response?.status !== 401 || !original || original._retried || isTokenEndpoint(original.url)) {
      return Promise.reject(error);
    }
    original._retried = true;
    refreshing ??= auth.refresh().finally(() => { refreshing = null; });
    const token = await refreshing;
    if (!token) {
      auth.expired();
      return Promise.reject(error);
    }
    original.headers.Authorization = `Bearer ${token}`;
    return axiosClient(original);
  },
);

export default axiosClient;
