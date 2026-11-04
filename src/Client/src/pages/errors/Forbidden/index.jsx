import ErrorScreen from "../NotFound/components/ErrorScreen";

export default function Forbidden() {
  return <ErrorScreen code="403" title="Bạn không có quyền truy cập trang này" text="Trang này dành cho vai trò khác. Hãy quay lại trang chủ của bạn." />;
}
