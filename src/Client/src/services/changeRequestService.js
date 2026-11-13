import axiosClient from "../api/axiosClient";
import { changeRequestRoutes } from "../api/endpoints";

const unwrap = (request) => request.then((r) => r.data);

// FE-05: C-05 (list), C-06 (form/create/edit draft), M-07, M-08 (detail, counter-proposal, decisions).
// `role` is "Customer" or "Manager"; the Manager calls also pass the project code.
export const changeRequestService = {
  getList: (role, projectCode) => unwrap(axiosClient.get(changeRequestRoutes(role, projectCode).list)),
  getForm: (role, projectCode) => unwrap(axiosClient.get(changeRequestRoutes(role, projectCode).form)),
  getDetail: (role, projectCode, crCode) => unwrap(axiosClient.get(changeRequestRoutes(role, projectCode).detail(crCode))),
  create: (role, projectCode, body) => unwrap(axiosClient.post(changeRequestRoutes(role, projectCode).create, body)),
  updateDraft: (role, projectCode, crCode, body) => unwrap(axiosClient.put(changeRequestRoutes(role, projectCode).detail(crCode), body)),
  submit: (role, projectCode, crCode) => unwrap(axiosClient.post(changeRequestRoutes(role, projectCode).submit(crCode))),
  counter: (projectCode, crCode, body) => unwrap(axiosClient.post(changeRequestRoutes("Manager", projectCode).counter(crCode), body)),
  approve: (role, projectCode, crCode, note) =>
    unwrap(axiosClient.post(changeRequestRoutes(role, projectCode).approve(crCode), { note })),
  reject: (role, projectCode, crCode, reason) =>
    unwrap(axiosClient.post(changeRequestRoutes(role, projectCode).reject(crCode), { reason })),
  requestException: (projectCode, crCode, note) =>
    unwrap(axiosClient.post(changeRequestRoutes("Manager", projectCode).exception(crCode), { note })),
};
