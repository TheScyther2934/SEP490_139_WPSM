import { Link as RouterLink } from "react-router-dom";
import { Alert, Button } from "@mui/material";
import { useAsync } from "../../../../hooks/useAsync";
import { dayOfService } from "../../../../services/operationsService";
import { taskStats } from "../../../../components/coordination/departments";
import { PATHS } from "../../../../routes/paths";

// Before settling with the customer: coordination tasks not done, or done without the proof they require.
// A reminder only; settlement is not blocked.
export default function CoordinationWarning({ projectCode }) {
  const board = useAsync(() => dayOfService.get(projectCode), [projectCode]);
  const tasks = board.data?.tasks ?? [];
  if (!tasks.length) return null;
  const s = taskStats(tasks);
  const open = s.total - s.done;
  if (!open && !s.missing) return null;
  const parts = [open && `${open} nhiệm vụ chưa xong`, s.missing && `${s.missing} nhiệm vụ thiếu ảnh hoặc tài liệu chứng minh`].filter(Boolean);
  return (
    <Alert severity="warning" sx={{ mb: 2 }}
      action={<Button color="inherit" component={RouterLink} to={PATHS.manager.departmentSheets(projectCode)}>Xem</Button>}>
      Điều phối còn {parts.join(" và ")}. Nên bổ sung trước khi tất toán để có bằng chứng nếu khách khiếu nại.
    </Alert>
  );
}
