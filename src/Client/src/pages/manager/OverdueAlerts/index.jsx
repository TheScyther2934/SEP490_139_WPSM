import { useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Box,
  Button,
  Card,
  Chip,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { managerService } from "../../../services/managerService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi } from "../../../utils/format";

// M-23: Overdue Alerts (Manager) - UC-32
export default function ManagerOverdueAlerts() {
  const { data, loading, error, reload } = useAsync(() => managerService.getOverdueAlerts());
  usePolling(() => reload({ silent: true }), 30000);

  const alerts = data ?? [];

  return (
    <>
      <PageHeader
        code="M-23"
        title="Cảnh báo quá hạn"
        subtitle="Theo dõi và điều phối các nhiệm vụ ngày sự kiện đã quá hạn trong những dự án bạn phụ trách."
        actions={
          <Button
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={() => reload()}
            disabled={loading}
          >
            Làm mới
          </Button>
        }
      />

      <Box sx={{ mb: 2 }}>
        <Chip
          icon={<WarningAmberIcon />}
          label={`Tổng số cảnh báo: ${alerts.length} nhiệm vụ quá hạn`}
          color={alerts.length > 0 ? "error" : "success"}
          variant="outlined"
          sx={{ fontWeight: "bold" }}
        />
      </Box>

      <DataState
        loading={loading}
        error={error}
        onRetry={reload}
        empty={!alerts.length}
        emptyMessage="Không có nhiệm vụ nào bị quá hạn (MSG01)."
      >
        <Card>
          <TableContainer>
            <Table id="m23Table">
              <TableHead>
                <TableRow>
                  <TableCell>Mã dự án</TableCell>
                  <TableCell>Nhiệm vụ</TableCell>
                  <TableCell>Sảnh tiệc</TableCell>
                  <TableCell>Ngày sự kiện</TableCell>
                  <TableCell>Hạn chót</TableCell>
                  <TableCell>Thời gian quá hạn</TableCell>
                  <TableCell align="center">Trạng thái</TableCell>
                  <TableCell align="right">Hành động</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {alerts.map((a) => (
                  <TableRow key={a.taskId} hover>
                    <TableCell sx={{ fontWeight: "bold" }}>
                      <RouterLink
                        to={PATHS.manager.project(a.projectCode)}
                        style={{ color: "inherit", textDecoration: "none" }}
                      >
                        {a.projectCode}
                      </RouterLink>
                      {a.customerName && (
                        <Typography variant="caption" display="block" color="text.secondary">
                          {a.customerName}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>{a.taskName}</TableCell>
                    <TableCell>{a.hall || "—"}</TableCell>
                    <TableCell>{a.eventDate ? dateVi(a.eventDate) : "—"}</TableCell>
                    <TableCell>{dateTimeVi(a.deadline)}</TableCell>
                    <TableCell>
                      <Typography color="error.main" fontWeight="bold">
                        {a.overdueMinutes} phút
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <Chip label="Quá hạn" color="error" size="small" sx={{ fontWeight: "bold" }} />
                    </TableCell>
                    <TableCell align="right">
                      <Button
                        size="small"
                        variant="contained"
                        color="error"
                        endIcon={<ArrowForwardIcon />}
                        component={RouterLink}
                        to={`${PATHS.manager.dayOf(a.projectCode)}?taskId=${a.taskId}`}
                      >
                        Mở Day-of Board
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Card>
      </DataState>
    </>
  );
}
