import { useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import {
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
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi } from "../../../utils/format";

// M-18: Manager Complaints List for Project (UC-35)
export default function ManagerComplaints() {
  const { projectCode } = useParams();
  const [statusFilter, setStatusFilter] = useState("ALL");

  const { data, loading, error, reload } = useAsync(
    () => projectService.getComplaints(projectCode),
    [projectCode]
  );

  const filtered = (data || []).filter((item) => statusFilter === "ALL" || item.status === statusFilter);

  return (
    <>
      <PageHeader
        code="M-18"
        title={`Quản lý Khiếu nại Dịch vụ (${projectCode})`}
        subtitle="Tiếp nhận, đối chiếu BEO cam kết, phản hồi giải quyết hoặc chuyển cấp lên Admin đối với các khiếu nại vượt thẩm quyền."
        actions={
          <Stack direction="row" spacing={1.5} alignItems="center">
            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel>Lọc trạng thái</InputLabel>
              <Select
                label="Lọc trạng thái"
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
              >
                <MenuItem value="ALL">Tất cả ({data?.length || 0})</MenuItem>
                <MenuItem value="Open">Đang xử lý (Open)</MenuItem>
                <MenuItem value="Escalated">Đã chuyển Admin (Escalated)</MenuItem>
                <MenuItem value="Resolved">Đã giải quyết (Resolved)</MenuItem>
              </Select>
            </FormControl>
            <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.project(projectCode)}>
              Về Project Hub
            </Button>
          </Stack>
        }
      />

      <DataState
        loading={loading}
        error={error}
        onRetry={reload}
        empty={!filtered.length}
        emptyText="Không có hồ sơ khiếu nại nào phù hợp với bộ lọc."
      >
        <Paper variant="outlined">
          <TableContainer>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>Mã khiếu nại</TableCell>
                  <TableCell>Khách hàng</TableCell>
                  <TableCell>BEO đối chiếu</TableCell>
                  <TableCell>Nội dung khiếu nại</TableCell>
                  <TableCell>Ngày tiếp nhận</TableCell>
                  <TableCell>Trạng thái</TableCell>
                  <TableCell align="right">Thao tác</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filtered.map((item) => (
                  <TableRow key={item.complaintCode} hover>
                    <TableCell sx={{ fontWeight: 800 }}>{item.complaintCode}</TableCell>
                    <TableCell>{item.coupleName}</TableCell>
                    <TableCell>
                      <Chip size="small" label={`BEO v${item.beoVersion}`} variant="outlined" />
                    </TableCell>
                    <TableCell sx={{ maxWidth: 380 }}>
                      <Typography variant="body2" noWrap title={item.description}>
                        {item.description}
                      </Typography>
                    </TableCell>
                    <TableCell>{dateTimeVi(item.createdAt)}</TableCell>
                    <TableCell>
                      <StatusChip kind="complaint" status={item.status} />
                    </TableCell>
                    <TableCell align="right">
                      <Button
                        size="small"
                        variant={item.status === "Open" ? "contained" : "outlined"}
                        startIcon={<VisibilityOutlinedIcon />}
                        component={RouterLink}
                        to={PATHS.manager.complaint(projectCode, item.complaintCode)}
                      >
                        {item.status === "Open" ? "Xử lý (M-19)" : "Xem chi tiết"}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      </DataState>
    </>
  );
}
