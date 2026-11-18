import { PATHS, ROLES } from "../routes/paths";

/**
 * Resolves a safe internal application route for a notification based on user's role,
 * the notification's type, linkUrl, and entity references.
 *
 * Prevents external URL redirect or navigating to forbidden screens.
 *
 * @param {Object} notification
 * @param {string} role "Customer" | "Manager" | "Admin"
 * @returns {string | null} Target route or null if not navigable.
 */
export function resolveNotificationRoute(notification, role) {
  if (!notification) return null;

  const { type, linkUrl, relatedEntityType, relatedEntityId } = notification;

  // 1. If linkUrl is provided and is a safe internal relative path (starts with /)
  if (linkUrl && typeof linkUrl === "string") {
    const trimmed = linkUrl.trim();

    // Security check: Never redirect to external URLs or protocols
    if (trimmed.startsWith("http://") || trimmed.startsWith("https://") || trimmed.startsWith("//")) {
      return null;
    }

    // Handle legacy short codes if present in database (e.g. /C03, /C01, /M04?id=XYZ)
    if (role === ROLES.CUSTOMER) {
      if (trimmed.startsWith("/C03")) return PATHS.customer.roadmap;
      if (trimmed.startsWith("/C01")) return PATHS.customer.overview;
      if (trimmed.startsWith("/C02")) return PATHS.customer.contract;
      if (trimmed.startsWith("/C05")) return PATHS.customer.changeRequests;
      if (trimmed.startsWith("/C10")) return PATHS.customer.payments;
      if (trimmed.startsWith("/C14")) return PATHS.customer.complaints;
      if (trimmed.startsWith("/customer/")) return trimmed;
    } else if (role === ROLES.MANAGER) {
      if (trimmed.startsWith("/M04")) {
        const match = trimmed.match(/[?&]id=([^&#]+)/i);
        const code = match ? decodeURIComponent(match[1]) : null;
        return code ? PATHS.manager.project(code) : PATHS.manager.projects;
      }
      if (trimmed.startsWith("/M06")) {
        const match = trimmed.match(/[?&]id=([^&#]+)/i);
        const code = match ? decodeURIComponent(match[1]) : null;
        return code ? PATHS.manager.roadmap(code) : PATHS.manager.projects;
      }
      if (trimmed.startsWith("/manager/")) return trimmed;
    }
  }

  // 2. Resolve by type & role fallback
  const normalizedType = (type || "").toLowerCase().trim();
  const projectCode = relatedEntityId || extractProjectCode(linkUrl);

  if (role === ROLES.CUSTOMER) {
    switch (normalizedType) {
      case "roadmap reminder":
      case "roadmap":
        return PATHS.customer.roadmap;

      case "payment":
        return PATHS.customer.payments;

      case "cr update":
      case "cr":
      case "changerequest":
        return PATHS.customer.changeRequests;

      case "beo":
        return PATHS.customer.contract;

      case "complaint":
        return PATHS.customer.complaints;

      default:
        return PATHS.customer.overview;
    }
  }

  if (role === ROLES.MANAGER) {
    switch (normalizedType) {
      case "roadmap reminder":
      case "roadmap":
        return projectCode ? PATHS.manager.roadmap(projectCode) : PATHS.manager.projects;

      case "payment":
        return projectCode ? PATHS.manager.payments(projectCode) : PATHS.manager.projects;

      case "cr update":
      case "cr":
      case "changerequest":
        return projectCode ? PATHS.manager.changeRequests(projectCode) : PATHS.manager.projects;

      case "beo":
        return projectCode ? PATHS.manager.beo(projectCode) : PATHS.manager.projects;

      case "overdue alert":
      case "overdue":
        return projectCode ? PATHS.manager.dayOf(projectCode) : PATHS.manager.overdueAlerts;

      case "complaint":
        return projectCode ? PATHS.manager.complaints(projectCode) : PATHS.manager.projects;

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
