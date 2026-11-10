import { Link as RouterLink, useParams } from "react-router-dom";
import {
  Alert,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  Grid,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, money } from "../../../utils/format";

// C-16: Customer Complaint Status & Resolution Detail (UC-16)
export default function ComplaintStatus() {
  const { complaintCode } = useParams();
  const { data: item, loading, error, reload } = useAsync(
    () => customerService.getComplaint(complaintCode),
    [complaintCode]
  );

  return (
    <>
      <PageHeader
        code="C-16"
        title={`Trạng thái & Kết quả Giải quyết Khiếu nại (${complaintCode})`}
        subtitle="Theo dõi tiến trình xử lý của Quản lý dự án hoặc quyết định cuối cùng từ Ban Quản trị (Admin)."
        actions={
          <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.complaints}>
            Danh sách khiếu nại (C-14)
          </Button>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload} empty={!item} emptyText="Không tìm thấy hồ sơ khiếu nại.">
        {item && (
          <Stack spacing={3}>
            {item.status === "Resolved" && (
              <Alert severity="success">
                Hồ sơ khiếu nại <strong>{item.complaintCode}</strong> đã được giải quyết hoàn tất vào lúc{" "}
                <strong>{dateTimeVi(item.resolvedAt)}</strong> ({item.resolvedByName || "WPSM"}).
              </Alert>
            )}
            {item.status === "Escalated" && (
              <Alert severity="warning">
                Hồ sơ khiếu nại đã được Quản lý chuyển cấp lên <strong>Ban Quản trị (Admin)</strong> vào lúc{" "}
                <strong>{dateTimeVi(item.escalatedAt)}</strong> để thẩm định và ra quyết định cuối cùng.
              </Alert>
            )}

            <Grid container spacing={2.5}>
              <Grid item xs={12} md={6}>
                <Card variant="outlined" sx={{ height: "100%" }}>
                  <CardContent>
                    <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                      <Typography variant="subtitle1" fontWeight={800}>
                        Nội dung Khiếu nại của Khách hàng
                      </Typography>
                      <StatusChip kind="complaint" status={item.status} />
                    </Stack>
                    <Divider sx={{ mb: 2 }} />
                    <Stack spacing={1.5}>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Mã hồ sơ:</Typography>
                        <Typography fontWeight={800}>{item.complaintCode}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Dự án & Ngày tiệc:</Typography>
                        <Typography fontWeight={700}>
                          {item.projectCode} · {item.hallName} ({dateVi(item.eventDate)})
                        </Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">BEO đối chiếu:</Typography>
                        <Chip size="small" label={`BEO v${item.beoVersion} (${item.beoStatus})`} color="primary" variant="outlined" />
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Thời점 gửi:</Typography>
                        <Typography variant="body2">{dateTimeVi(item.createdAt)}</Typography>
                      </Stack>
                      <Divider />
                      <Typography variant="caption" color="text.secondary">Mô tả chi tiết của gia đình:</Typography>
                      <Typography sx={{ whiteSpace: "pre-wrap" }}>{item.description}</Typography>
                    </Stack>
                  </CardContent>
                </Card>
              </Grid>

              <Grid item xs={12} md={6}>
                <Card variant="outlined" sx={{ height: "100%", bgcolor: "grey.50" }}>
                  <CardContent>
                    <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                      Phản hồi & Quyết định Giải quyết
                    </Typography>
                    <Divider sx={{ mb: 2 }} />
                    <Stack spacing={2}>
                      {item.escalationNote && (
                        <Paper variant="outlined" sx={{ p: 1.5, bgcolor: "warning.50" }}>
                          <Typography variant="caption" fontWeight={700} color="warning.dark">
                            Ghi chú chuyển cấp của Quản lý ({dateTimeVi(item.escalatedAt)}):
                          </Typography>
                          <Typography variant="body2" sx={{ mt: 0.5 }}>
                            {item.escalationNote}
                          </Typography>
                        </Paper>
                      )}

                      {item.resolutionNote ? (
                        <Paper variant="outlined" sx={{ p: 2, bgcolor: "background.paper" }}>
                          <Typography variant="caption" fontWeight={700} color="success.main">
                            Phương án giải quyết chính thức ({dateTimeVi(item.resolvedAt)}):
                          </Typography>
                          <Typography variant="body1" fontWeight={600} sx={{ mt: 0.75, whiteSpace: "pre-wrap" }}>
                            {item.resolutionNote}
                          </Typography>
                        </Paper>
                      ) : (
                        <Typography color="text.secondary">
                          Đội ngũ Quản lý dự án đang đối chiếu biên bản BEO v{item.beoVersion} và dữ liệu hình ảnh hiện trường ({item.proofPhotosCount} ảnh nghiệm thu) để phản hồi sớm nhất cho gia đình.
                        </Typography>
                      )}
                    </Stack>
                  </CardContent>
                </Card>
              </Grid>
            </Grid>

            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                Bảng Hạng mục Cam kết trong BEO v{item.beoVersion} (Cơ sở đối chiếu)
              </Typography>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Bộ phận</TableCell>
                      <TableCell>Hạng mục dịch vụ</TableCell>
                      <TableCell>Mô tả cam kết</TableCell>
                      <TableCell align="right">Số lượng BEO</TableCell>
                      <TableCell align="right">Thành tiền</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {item.disputedBeoItems?.map((line) => (
                      <TableRow key={line.specItemId}>
                        <TableCell>{line.department}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{line.itemName}</TableCell>
                        <TableCell>{line.description}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>
                          {line.effectiveQuantity} {line.unit}
                        </TableCell>
                        <TableCell align="right">{money(line.lineTotal)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>
          </Stack>
        )}
      </DataState>
    </>
  );
}
