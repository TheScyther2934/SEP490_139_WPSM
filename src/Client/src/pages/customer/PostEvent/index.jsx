import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Divider,
  Grid,
  Rating as MuiRating,
  Stack,
  Typography,
} from "@mui/material";
import StarBorderIcon from "@mui/icons-material/StarBorder";
import ReportProblemOutlinedIcon from "@mui/icons-material/ReportProblemOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, money, slotLabel } from "../../../utils/format";

// C-12: Post-event Summary & Entry to Service Ratings (C-13) and Complaints (C-14/C-15)
export default function PostEvent() {
  const { data, loading, error, reload } = useAsync(() => customerService.getPostEvent(), []);

  return (
    <>
      <PageHeader
        code="C-12"
        title="Tổng kết Sau Tiệc & Đánh giá Dịch vụ"
        subtitle="Xem tổng kết thực hiện hợp đồng sau ngày tiệc, gửi đánh giá chất lượng 4 hạng mục dịch vụ hoặc gửi khiếu nại đối chiếu BEO."
        actions={
          <Stack direction="row" spacing={1.5}>
            <Button
              variant="outlined"
              color="warning"
              startIcon={<ReportProblemOutlinedIcon />}
              component={RouterLink}
              to={PATHS.customer.complaints}
            >
              Khiếu nại dịch vụ (C-14)
            </Button>
            <Button
              variant="contained"
              startIcon={<StarBorderIcon />}
              component={RouterLink}
              to={PATHS.customer.ratings}
            >
              {data?.isLocked ? "Xem đánh giá đã gửi (C-13)" : "Đánh giá 4 hạng mục (C-13)"}
            </Button>
          </Stack>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload} empty={!data} emptyText="Chưa có dữ liệu dự án cưới.">
        {data && (
          <Stack spacing={3}>
            {data.isLocked ? (
              <Alert severity="success" icon={<CheckCircleOutlineIcon />}>
                Cảm ơn gia đình <strong>{data.coupleName}</strong> đã hoàn tất đánh giá dịch vụ tiệc cưới. Theo quy tắc{" "}
                <strong>GBR-10</strong>, kết quả đánh giá đã được khóa vĩnh viễn để đảm bảo tính minh bạch.
              </Alert>
            ) : (
              <Alert severity="info">
                Gia đình có thể gửi đánh giá chất lượng cho 4 hạng mục (<strong>Sảnh tiệc, Thực đơn, Trang trí, MC</strong>) tại màn hình{" "}
                <strong>C-13</strong>. Lưu ý: Đánh giá chỉ được gửi 1 lần duy nhất và không thể chỉnh sửa sau khi xác nhận (GBR-10).
              </Alert>
            )}

            <Grid container spacing={2.5}>
              <Grid item xs={12} md={6}>
                <Card variant="outlined" sx={{ height: "100%" }}>
                  <CardContent>
                    <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                      <Typography variant="subtitle1" fontWeight={800}>
                        Hồ sơ Sự kiện ({data.projectCode})
                      </Typography>
                      <StatusChip kind="project" status={data.projectStatus} />
                    </Stack>
                    <Divider sx={{ mb: 2 }} />
                    <Grid container spacing={2}>
                      <Grid item xs={6}>
                        <Typography variant="caption" color="text.secondary">Cặp đôi</Typography>
                        <Typography fontWeight={700}>{data.coupleName}</Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="caption" color="text.secondary">Sảnh & Ca tiệc</Typography>
                        <Typography fontWeight={700}>
                          {data.hallName} · Ca {slotLabel(data.slotName)}
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="caption" color="text.secondary">Ngày tổ chức</Typography>
                        <Typography fontWeight={700}>{dateVi(data.eventDate)}</Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="caption" color="text.secondary">Tiến độ vận hành ngày cưới</Typography>
                        <Typography fontWeight={700}>
                          {data.completedTasksCount}/{data.totalTasksCount} hạng mục hoàn thành
                        </Typography>
                      </Grid>
                    </Grid>
                  </CardContent>
                </Card>
              </Grid>

              <Grid item xs={12} md={6}>
                <Card variant="outlined" sx={{ height: "100%", bgcolor: "grey.50" }}>
                  <CardContent>
                    <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                      Tình trạng Tài chính & Khiếu nại
                    </Typography>
                    <Divider sx={{ mb: 2 }} />
                    <Stack spacing={1.2}>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">Tổng giá trị hợp đồng cuối:</Typography>
                        <Typography fontWeight={800}>{money(data.finalContractTotal)}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">Tổng số tiền đã thanh toán:</Typography>
                        <Typography fontWeight={700} color="success.main">
                          {money(data.paidTotal)}
                        </Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">Khiếu nại đang mở:</Typography>
                        <Typography fontWeight={700} color={data.openComplaintsCount > 0 ? "warning.dark" : "text.primary"}>
                          {data.openComplaintsCount} hồ sơ
                        </Typography>
                      </Stack>
                    </Stack>
                  </CardContent>
                </Card>
              </Grid>
            </Grid>

            <Card variant="outlined">
              <CardContent>
                <Stack direction="row" justifyContent="space-between" alignItems="center" mb={2}>
                  <Typography variant="subtitle1" fontWeight={800}>
                    Kết quả Đánh giá 4 Hạng mục Dịch vụ
                  </Typography>
                  {!data.isLocked && (
                    <Button size="small" variant="contained" component={RouterLink} to={PATHS.customer.ratings}>
                      Thực hiện đánh giá ngay
                    </Button>
                  )}
                </Stack>
                <Grid container spacing={2}>
                  {data.ratings?.map((r) => (
                    <Grid item xs={12} sm={6} key={r.module}>
                      <Box
                        sx={{
                          p: 2,
                          borderRadius: 1.5,
                          border: "1px solid",
                          borderColor: "divider",
                          bgcolor: r.stars ? "background.paper" : "grey.50",
                        }}
                      >
                        <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1}>
                          <Typography fontWeight={700}>{r.moduleLabel}</Typography>
                          <MuiRating value={r.stars || 0} readOnly size="small" />
                        </Stack>
                        <Typography variant="body2" color="text.secondary">
                          {r.comment || (r.stars ? "Không có nhận xét bổ sung." : "Chưa gửi đánh giá.")}
                        </Typography>
                        {r.ratedAt && (
                          <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 0.75 }}>
                            Đã gửi lúc: {dateTimeVi(r.ratedAt)}
                          </Typography>
                        )}
                      </Box>
                    </Grid>
                  ))}
                </Grid>
              </CardContent>
            </Card>
          </Stack>
        )}
      </DataState>
    </>
  );
}
