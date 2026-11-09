import { Link as RouterLink, useParams } from "react-router-dom";
import {
  Alert,
  Button,
  Card,
  CardContent,
  Divider,
  Grid,
  Stack,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import CalculateOutlinedIcon from "@mui/icons-material/CalculateOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, money, signedMoney } from "../../../utils/format";

// M-08: Manager Change Request Detail
export default function ManagerChangeRequestDetail() {
  const { projectCode, crCode } = useParams();
  const { data: cr, loading, error, reload } = useAsync(
    () => projectService.getCostImpact(projectCode, crCode),
    [projectCode, crCode]
  );

  return (
    <>
      <PageHeader
        code="M-08"
        title={`Chi tiết Yêu cầu Thay đổi (${crCode})`}
        subtitle="Xem nội dung đề xuất của khách hàng và chuyển sang màn hình thẩm định tác động chi phí (M-09)."
        actions={
          <Stack direction="row" spacing={1.5}>
            <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.changeRequests(projectCode)}>
              Danh sách CR (M-07)
            </Button>
            <Button
              variant="contained"
              startIcon={<CalculateOutlinedIcon />}
              component={RouterLink}
              to={PATHS.manager.costImpact(projectCode, crCode)}
            >
              Tác động chi phí & Lập phụ lục (M-09)
            </Button>
          </Stack>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload} empty={!cr} emptyText="Không tìm thấy yêu cầu thay đổi.">
        {cr && (
          <Card variant="outlined">
            <CardContent>
              <Stack direction="row" justifyContent="space-between" alignItems="center" mb={2}>
                <Typography variant="h6" fontWeight={800}>
                  {cr.crCode} · {cr.type}
                </Typography>
                <StatusChip kind="cr" status={cr.status} />
              </Stack>
              <Divider sx={{ mb: 2 }} />
              <Grid container spacing={2}>
                <Grid item xs={12} sm={4}>
                  <Typography variant="caption" color="text.secondary">Ngày sự kiện</Typography>
                  <Typography fontWeight={700}>{dateVi(cr.eventDate)} ({cr.daysUntilEvent >= 0 ? `T-${cr.daysUntilEvent}` : ""})</Typography>
                </Grid>
                <Grid item xs={12} sm={4}>
                  <Typography variant="caption" color="text.secondary">Chênh lệch dịch vụ</Typography>
                  <Typography fontWeight={700}>{signedMoney(cr.costImpact)}</Typography>
                </Grid>
                <Grid item xs={12} sm={4}>
                  <Typography variant="caption" color="text.secondary">Tổng tác động (gồm phạt)</Typography>
                  <Typography fontWeight={800} color="primary.main">{signedMoney(cr.totalVariance)}</Typography>
                </Grid>
                <Grid item xs={12}>
                  <Typography variant="caption" color="text.secondary">Nội dung yêu cầu</Typography>
                  <Typography sx={{ mt: 0.5, whiteSpace: "pre-wrap" }}>{cr.description}</Typography>
                </Grid>
              </Grid>
            </CardContent>
          </Card>
        )}
      </DataState>
    </>
  );
}
