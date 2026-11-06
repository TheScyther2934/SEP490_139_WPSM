import axiosClient from "../api/axiosClient";
import { ENDPOINTS } from "../api/endpoints";

export const notificationService = {
  getNotifications: (filter = "All", page = 1, pageSize = 20) =>
    axiosClient
      .get(ENDPOINTS.notifications.list(filter, page, pageSize))
      .then((r) => r.data),

  getUnreadCount: () =>
    axiosClient
      .get(ENDPOINTS.notifications.unreadCount)
      .then((r) => r.data?.unreadCount ?? 0),

  markAsRead: (notificationId) =>
    axiosClient
      .patch(ENDPOINTS.notifications.markAsRead(notificationId))
      .then((r) => r.data),

  markAllAsRead: () =>
    axiosClient
      .patch(ENDPOINTS.notifications.markAllAsRead)
      .then((r) => r.data),
};
