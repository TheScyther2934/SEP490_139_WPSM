import ErrorScreen from "./components/ErrorScreen";

export default function NotFound() {
  return <ErrorScreen code="404" title="Không tìm thấy trang" text="Đường dẫn không tồn tại hoặc đã thay đổi." />;
}
