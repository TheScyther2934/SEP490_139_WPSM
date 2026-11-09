import { useMemo, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Box,
  Button,
  Chip,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";
import { PATHS } from "../../../routes/paths";
import { dateVi, money, signedMoney } from "../../../utils/format";

// C-05: Customer Change Requests List (SRS §4.1: Filter by Status + Table + Create CR C-06 + Open C-07/C-08)
export default function CustomerChangeRequests() {
  const { data: crs, loading, error, reload } = useAsync(() => customerService.getChangeRequests(), []);
  const [statusFilter, setStatusFilter] = useState("All");

  const filteredCrs = useMemo(() => {
    if (!crs) return [];
    if (statusFilter === "All") return crs;
    return crs.filter((item) => item.status === statusFilter);
  }, [crs, statusFilter]);

  return (
    <>
      <PageHeader
        code="C-05"
        title="Danh sách Yêu cầu Thay đổi (Change Requests)"
        subtitle="Theo dõi các đề xuất điều chỉnh thực đơn, số bàn, trang trí... do bạn gửi hoặc báo giá điều chỉnh từ Quản lý dự án."
        actions={
          <Stack direction="row" spacing={1.5} alignItems="center">
            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="cr-status-filter-label">Lọc trạng thái</InputLabel>
              <Select
                labelId="cr-status-filter-label"
                label="Lọc trạng thái"
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
              >
                <MenuItem value="All">Tất cả trạng thái</MenuItem>
                <MenuItem value="Draft">Bản nháp (Draft)</MenuItem>
                <MenuItem value="Negotiating">Đang thương lượng</MenuItem>
                <MenuItem value="Approved">Đã phê duyệt</MenuItem>
                <MenuItem value="Rejected">Đã từ chối</MenuItem>
              </Select>
            </FormControl>
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              component={RouterLink}
              to={PATHS.customer.newChangeRequest}
              sx={{ whiteSpace: "nowrap" }}
            >
              Tạo yêu cầu thay đổi (C-06)
            </Button>
          </Stack>
        }
      />

      <DataState
        loading={loading}
        error={error}
        onRetry={reload}
        empty={!filteredCrs.length}
        emptyText="Không có yêu cầu thay đổi nào phù hợp."
      >
        <Paper variant="outlined">
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ whiteSpace: "nowrap" }}>Mã CR / Loại</TableCell>
                  <TableCell>Mô tả thay đổi</TableCell>
                  <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>Chênh lệch</TableCell>
                  <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>Phí T-day</TableCell>
                  <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>Tổng tác động</TableCell>
                  <TableCell sx={{ whiteSpace: "nowrap" }}>Ngày gửi</TableCell>
                  <TableCell sx={{ whiteSpace: "nowrap" }}>Trạng thái xử lý</TableCell>
                  <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>Thao tác</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredCrs.map((item) => {
                  // Only when Manager has sent a quote/counter-proposal and Customer hasn't confirmed yet
                  const waitingForCustomer =
                    item.status === "Negotiating" && item.managerApprovedAt && !item.customerApprovedAt;
                  const waitingForManager =
                    item.status === "Negotiating" && item.customerApprovedAt && !item.managerApprovedAt;

                  return (
                    <TableRow key={item.crCode} hover>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>
                        <Typography variant="body2" fontWeight={800}>
                          {item.crCode}
                        </Typography>
                        <Chip
                          size="small"
                          label={item.type}
                          variant="outlined"
                          sx={{ mt: 0.5, height: 20, fontSize: "0.72rem" }}
                        />
                      </TableCell>
                      <TableCell sx={{ minWidth: 180, maxWidth: 260 }}>
                        <Box
                          component="span"
                          title={item.description}
                          sx={{
                            display: "-webkit-box",
                            WebkitLineClamp: 2,
                            WebkitBoxOrient: "vertical",
                            overflow: "hidden",
                            fontSize: "0.875rem",
                          }}
                        >
                          {item.description}
                        </Box>
                      </TableCell>
                      <TableCell align="right" sx={{ fontWeight: 700, whiteSpace: "nowrap" }}>
                        {signedMoney(item.costImpact)}
                      </TableCell>
                      <TableCell
                        align="right"
                        sx={{
                          whiteSpace: "nowrap",
                          color: item.penaltyFee > 0 ? "warning.dark" : "text.secondary",
                        }}
                      >
                        {money(item.penaltyFee)}
                      </TableCell>
                      <TableCell
                        align="right"
                        sx={{
                          fontWeight: 800,
                          whiteSpace: "nowrap",
                          color: item.totalVariance >= 0 ? "primary.main" : "success.main",
                        }}
                      >
                        {signedMoney(item.totalVariance)}
                      </TableCell>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>{dateVi(item.createdAt)}</TableCell>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>
                        <Stack spacing={0.5} alignItems="flex-start">
                          <StatusChip kind="cr" status={item.status} />
                          {waitingForManager && (
                            <Typography variant="caption" color="text.secondary">
                              Chờ Quản lý duyệt
                            </Typography>
                          )}
                          {waitingForCustomer && (
                            <Typography variant="caption" color="warning.dark" fontWeight={600}>
                              QL đã báo giá · Chờ bạn xác nhận
                            </Typography>
                          )}
                        </Stack>
                      </TableCell>
                      <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                        <Button
                          size="small"
                          variant={waitingForCustomer ? "contained" : "outlined"}
                          startIcon={<VisibilityOutlinedIcon />}
                          component={RouterLink}
                          to={PATHS.customer.changeRequest(item.crCode)}
                          id={`viewCr_${item.crCode}`}
                          sx={{ whiteSpace: "nowrap" }}
                        >
                          {waitingForCustomer ? "Xác nhận báo giá (C-08)" : "Chi tiết (C-07)"}
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      </DataState>
    </>
  );
}
