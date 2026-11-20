// Files people upload (proof, receipts, supplier documents). The server checks the content again and converts
// iPhone HEIC / WebP photos to JPEG (Cloudinary), so phone photos can be chosen as they are.
export const MAX_FILE_BYTES = 10 * 1024 * 1024;
export const PHOTO_ACCEPT = "image/*,.heic,.heif";
export const FILE_ACCEPT = "image/*,.heic,.heif,application/pdf,.pdf";
export const FILE_TYPES_LABEL = "Ảnh JPG, PNG, HEIC (iPhone), WebP hoặc PDF, tối đa 10 MB";

const ALLOWED = /\.(jpe?g|png|heic|heif|webp|avif|pdf)$/i;
export const isAllowedFile = (file) => !!file && file.size > 0 && file.size <= MAX_FILE_BYTES && ALLOWED.test(file.name);
export const isPdfFile = (file) => /\.pdf$/i.test(file?.name ?? "");
// Most browsers other than Safari cannot draw HEIC: show an icon instead of a broken preview.
export const canPreviewImage = (file) => /\.(jpe?g|png|webp|gif)$/i.test(file?.name ?? "");
