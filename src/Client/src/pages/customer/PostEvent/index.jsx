import { Link as RouterLink } from "react-router-dom";
import { Alert, Box, Button, Card, CardContent, Typography } from "@mui/material";
import StarIcon from "@mui/icons-material/Star";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync } from "../../../hooks/useAsync";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { feedbackService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";

// C-12: entry to ratings (UC-14) and complaints (UC-15); both open only after settlement (GBR-10).
export default function PostEvent() {
  const { project, loading: projectLoading, error: projectError } = useCustomerProject();
  const { data, loading, error, reload } = useAsync(() => (project ? feedbackService.postEvent(project.id) : Promise.resolve(null)), [project?.id]);
  return (
    <>
      <PageHeader code="C-12" title="Sau sự kiện" subtitle="Đánh giá dịch vụ và gửi khiếu nại sau khi dự án đã quyết toán." />
      <DataState loading={projectLoading || loading} error={projectError || error} onRetry={reload} empty={!data} emptyText="Tài khoản chưa có dự án.">
        {data && (
          <>
            {!data.canRate && data.ratings.length === 0 && (
              <Alert severity="info" sx={{ mb: 2 }}>
                Dự án đang ở trạng thái <StatusChip kind="project" status={data.projectStatus} />. Đánh giá và khiếu nại mở sau khi quyết toán.
              </Alert>
            )}
            <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { md: "1fr 1fr" } }}>
              <Card>
                <CardContent>
                  <StarIcon color="warning" />
                  <Typography variant="h6">Đánh giá dịch vụ</Typography>
                  <Typography color="text.secondary" sx={{ mb: 2 }}>Sảnh, thực đơn, trang trí và MC; mỗi hạng mục đánh giá một lần.</Typography>
                  <Button variant="contained" component={RouterLink} to={PATHS.customer.ratings} disabled={!data.canRate && data.ratings.length === 0}>
                    {data.ratings.length > 0 ? "Xem đánh giá" : "Đánh giá"}
                  </Button>
                </CardContent>
              </Card>
              <Card>
                <CardContent>
                  <ReportProblemIcon color="error" />
                  <Typography variant="h6">Khiếu nại</Typography>
                  <Typography color="text.secondary" sx={{ mb: 2 }}>Khi dịch vụ thực tế khác với BEO đã duyệt.</Typography>
                  <Button variant="outlined" component={RouterLink} to={PATHS.customer.complaints}>Danh sách khiếu nại</Button>
                </CardContent>
              </Card>
            </Box>
          </>
        )}
      </DataState>
    </>
  );
}
