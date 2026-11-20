import { PATHS, ROLES } from "../routes/paths";

/**
 * Resolves a safe internal application route for a notification based on the user's role,
 * the notification's type, linkUrl and entity references. Never navigates to external URLs.
 *
 * @param {Object} notification
 * @param {string} role "Customer" | "Manager" | "Admin"
 * @returns {string | null} Target route or null if not navigable.
 */
export function resolveNotificationRoute(notification, role) {
  if (!notification) return null;
  const { type, linkUrl, relatedEntityId } = notification;

  if (linkUrl && typeof linkUrl === "string") {
    const trimmed = linkUrl.trim();
    if (trimmed.startsWith("http://") || trimmed.startsWith("https://") || trimmed.startsWith("//")) return null;
    // Links of removed screens (roadmap, change requests, addenda) open the booked event / event overview instead.
    if (/\/(roadmap|change-requests|contract\/addenda)/.test(trimmed)) {
      if (role === ROLES.CUSTOMER) return PATHS.customer.event;
      const code = extractProjectCode(trimmed);
      return code ? PATHS.manager.project(code) : PATHS.manager.projects;
    }
    if (role === ROLES.CUSTOMER) {
      if (trimmed.startsWith("/C01")) return PATHS.customer.overview;
      if (trimmed.startsWith("/C02") || trimmed.startsWith("/C03") || trimmed.startsWith("/C05")) return PATHS.customer.event;
      if (trimmed.startsWith("/C10")) return PATHS.customer.payments;
      if (trimmed.startsWith("/C14")) return PATHS.customer.complaints;
      if (trimmed.startsWith("/customer/")) return trimmed;
    } else if (role === ROLES.MANAGER) {
      if (trimmed.startsWith("/M04") || trimmed.startsWith("/M06") || trimmed.startsWith("/M10")) {
        const match = trimmed.match(/[?&]id=([^&#]+)/i);
        const code = match ? decodeURIComponent(match[1]) : null;
        return code ? PATHS.manager.project(code) : PATHS.manager.projects;
      }
      if (trimmed.startsWith("/manager/")) return trimmed;
    } else if (trimmed.startsWith("/admin/")) {
      return trimmed;
    }
  }

  const normalizedType = (type || "").toLowerCase().trim();
  const projectCode = relatedEntityId || extractProjectCode(linkUrl);

  if (role === ROLES.CUSTOMER) {
    switch (normalizedType) {
      case "stage reminder":
      case "addon contract":
      case "beo":
        return PATHS.customer.event;
      case "payment":
        return PATHS.customer.payments;
      case "complaint":
        return PATHS.customer.complaints;
      default:
        return PATHS.customer.overview;
    }
  }

  if (role === ROLES.MANAGER) {
    const event = (to) => (projectCode ? to(projectCode) : PATHS.manager.projects);
    switch (normalizedType) {
      case "stage reminder":
        return event(PATHS.manager.project);
      case "addon contract":
        return event(PATHS.manager.addons);
      case "payment":
        return event(PATHS.manager.payments);
      case "beo":
        return event(PATHS.manager.beo);
      case "overdue alert":
        return projectCode ? PATHS.manager.dayOf(projectCode) : PATHS.manager.overdueAlerts;
      case "complaint":
        return event(PATHS.manager.complaints);
      case "delegation":
        return event(PATHS.manager.project);
      default:
        return PATHS.manager.dashboard;
    }
  }

  return null;
}

function extractProjectCode(linkUrl) {
  if (!linkUrl) return null;
  const match = linkUrl.match(/\/projects\/([^/?#]+)/i) || linkUrl.match(/[?&]id=([^&#]+)/i);
  return match ? decodeURIComponent(match[1]) : null;
}
