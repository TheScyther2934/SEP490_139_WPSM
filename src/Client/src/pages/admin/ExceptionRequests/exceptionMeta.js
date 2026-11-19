// A-18 helpers: special requests escalated to Admin (resource conflicts that cannot be solved inside the venue's capacity).

export const SOURCE_RESOURCE = "ResourceConflict";

export const sourceLabel = () => "Trùng lịch thiết bị";

export const sourceDescription = () => "Người phụ trách đề nghị giữ thiết bị, nhân sự vượt công suất.";

export const sourceColor = () => "info";

export const STATUS_ORDER = ["Pending", "Approved", "Rejected"];

export const statusLabel = (status) => ({ Pending: "Chờ duyệt", Approved: "Đã duyệt", Rejected: "Đã từ chối" }[status] ?? status);

/** Whole hours a pending request has been waiting (never negative). */
export const waitingHours = (createdAt) => Math.max(0, Math.floor((Date.now() - new Date(createdAt).getTime()) / 3_600_000));

export const waitingLabel = (createdAt) => {
    const hours = waitingHours(createdAt);
    if (hours < 1) return "Vừa gửi";
    if (hours < 24) return `Chờ ${hours} giờ`;
    return `Chờ ${Math.floor(hours / 24)} ngày`;
};

/** Short shortage line shown on the queue card. */
export const impactLabel = (request) =>
    `Thiếu ${request.resource?.shortage ?? 0}/${request.resource?.totalQuantity ?? 0} ${request.resource?.resourceTypeLabel ?? ""}`;

export const searchText = (request) => [
    request.code,
    request.projectCode,
    request.projectCouple,
    request.requestedByName,
    request.note,
    request.resource?.resourceName,
]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();
