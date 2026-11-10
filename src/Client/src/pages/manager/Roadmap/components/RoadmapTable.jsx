import {
  Box,
  Button,
  Card,
  Chip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import PersonIcon from "@mui/icons-material/Person";
import SupervisorAccountIcon from "@mui/icons-material/SupervisorAccount";
import StatusChip from "../../../../components/StatusChip";
import { dateVi, dateTimeVi } from "../../../../utils/format";

export default function RoadmapTable({ tasks, onCompleteTask }) {
  return (
    <Card elevation={1}>
      <TableContainer>
        <Table sx={{ minWidth: 650 }}>
          <TableHead sx={{ bgcolor: "background.neutral", "& th": { fontWeight: 600 } }}>
            <TableRow>
              <TableCell width="12%">Cột mốc</TableCell>
              <TableCell width="32%">Nhiệm vụ</TableCell>
              <TableCell width="16%">Phụ trách</TableCell>
              <TableCell width="14%">Hạn chót</TableCell>
              <TableCell width="12%">Trạng thái</TableCell>
              <TableCell width="14%" align="right">Thao tác</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {tasks.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} align="center" sx={{ py: 6, color: "text.secondary" }}>
                  <Typography variant="body1">Không có nhiệm vụ nào phù hợp với bộ lọc.</Typography>
                </TableCell>
              </TableRow>
            ) : (
              tasks.map((t) => {
                const isManager = t.assigneeRole === "Manager";
                const isDone = t.status === "Done";
                const isOverdue = t.status === "Overdue";

                return (
                  <TableRow
                    key={t.taskId ?? `${t.milestone}-${t.title}`}
                    hover
                    sx={{
                      bgcolor: isOverdue ? "error.lighter" : isDone ? "success.lighter" : "inherit",
                      transition: "background-color 0.2s ease"
                    }}
                  >
                    {/* Cột mốc */}
                    <TableCell>
                      <Chip
                        label={t.milestone}
                        size="small"
                        color={
                          t.milestone === "Event Day"
                            ? "secondary"
                            : t.milestone === "T-90"
                            ? "primary"
                            : "default"
                        }
                        sx={{ fontWeight: 600 }}
                      />
                    </TableCell>

                    {/* Tiêu đề nhiệm vụ */}
                    <TableCell>
                      <Typography variant="body2" fontWeight={500}>
                        {t.title}
                      </Typography>
                      {isDone && t.completedAt && (
                        <Typography variant="caption" color="text.secondary" display="block">
                          Hoàn thành: {dateTimeVi(t.completedAt)}
                        </Typography>
                      )}
                    </TableCell>

                    {/* Phụ trách */}
                    <TableCell>
                      <Chip
                        icon={isManager ? <SupervisorAccountIcon /> : <PersonIcon />}
                        label={isManager ? "Quản lý" : "Khách hàng"}
                        size="small"
                        variant="outlined"
                        color={isManager ? "primary" : "secondary"}
                        sx={{ fontWeight: 500 }}
                      />
                    </TableCell>

                    {/* Hạn chót */}
                    <TableCell>
                      <Typography
                        variant="body2"
                        color={isOverdue ? "error.main" : "text.primary"}
                        fontWeight={isOverdue ? 600 : 400}
                      >
                        {dateVi(t.dueDate)}
                      </Typography>
                    </TableCell>

                    {/* Trạng thái */}
                    <TableCell>
                      <StatusChip kind="task" status={t.status} />
                    </TableCell>

                    {/* Thao tác */}
                    <TableCell align="right">
                      {isDone ? (
                        <Box sx={{ display: "inline-flex", alignItems: "center", gap: 0.5, color: "success.main" }}>
                          <CheckCircleIcon fontSize="small" />
                          <Typography variant="caption" fontWeight={600}>
                            Đã xong
                          </Typography>
                        </Box>
                      ) : isManager ? (
                        <Button
                          variant="contained"
                          size="small"
                          color="primary"
                          onClick={() => onCompleteTask(t)}
                          sx={{ textTransform: "none", borderRadius: 1.5, px: 1.5 }}
                        >
                          Xác nhận xong
                        </Button>
                      ) : (
                        <Tooltip title="Nhiệm vụ thuộc trách nhiệm của Khách hàng">
                          <Typography variant="caption" color="text.secondary" sx={{ fontStyle: "italic" }}>
                            Khách hàng thực hiện
                          </Typography>
                        </Tooltip>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
