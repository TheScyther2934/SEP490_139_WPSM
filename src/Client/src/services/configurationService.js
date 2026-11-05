import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

// A-02..A-08: catalog, PriceFrames, policies (read by every role, changed by Admin)
export const configurationService = {
  getCatalog: () => axiosClient.get(ENDPOINTS.catalog).then((r) => r.data),
  addCatalogItem: (item) => axiosClient.post(ENDPOINTS.catalog, item).then((r) => r.data),
  getPriceFrames: () => axiosClient.get(ENDPOINTS.priceFrames).then((r) => r.data),
  updatePriceFrame: (id, body) => axiosClient.put(ENDPOINTS.priceFrame(id), body).then((r) => r.data),
  getPolicies: () => axiosClient.get(ENDPOINTS.policies).then((r) => r.data),
  updatePolicies: (body) => axiosClient.put(ENDPOINTS.policies, body).then((r) => r.data),
};
