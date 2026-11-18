import { money } from "../../../utils/format";

// A-18 helpers: the queue mixes CR penalty waivers (FE-05/FE-06) and resource conflicts (FE-07).

export const SOURCE_CR = "CrPenaltyWaiver";
export const SOURCE_RESOURCE = "ResourceConflict";

export const sourceLabel = (sourceType) => (sourceType === SOURCE_CR ? "Miễn phí phạt CR" : "Xung đột tài nguyên");

export const sourceDescription = (sourceType) => (sourceType === SOURCE_CR
    ? "Quản lý đề nghị bỏ phí phạt T-day cho một yêu cầu thay đổi."
    : "Quản lý đề nghị giữ chỗ thiết bị/nhân sự vượt công suất.");

export const sourceColor = (sourceType) => (sourceType === SOURCE_CR ? "secondary" : "info");

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

/** Short amount/shortage line shown on the queue card. */
export const impactLabel = (request) => (request.cr
    ? `Đề xuất miễn: ${money(request.cr.penaltyFee)}`
    : `Thiếu ${request.resource?.shortage ?? 0}/${request.resource?.totalQuantity ?? 0} ${request.resource?.resourceTypeLabel ?? ""}`);

export const searchText = (request) => [
    request.code,
    request.projectCode,
    request.projectCouple,
    request.requestedByName,
    request.note,
    request.cr?.crCode,
    request.resource?.resourceName,
]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();
