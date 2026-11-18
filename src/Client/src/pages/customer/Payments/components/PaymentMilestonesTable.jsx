import {
  Button,
  Card,
  CircularProgress,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import StatusChip from "../../../../components/StatusChip";
import { dateVi, money } from "../../../../utils/format";

export default function PaymentMilestonesTable({ milestones = [], onDownloadReceipt, downloadingType }) {
  return (
    <Card sx={{ mb: 3 }}>
      <TableContainer>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Đợt thanh toán</TableCell>
              <TableCell>Hạn thanh toán</TableCell>
              <TableCell align="right">Số tiền dự kiến</TableCell>
              <TableCell align="right">Đã thu</TableCell>
              <TableCell>Trạng thái</TableCell>
              <TableCell>Phương thức</TableCell>
              <TableCell>Ngày thu</TableCell>
              <TableCell align="center">Biên nhận</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {milestones.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} align="center">
                  Chưa có dữ liệu thanh toán.
                </TableCell>
              </TableRow>
            ) : (
              milestones.map((m) => {
                const isDownloading = downloadingType === m.paymentType;
                return (
                  <TableRow key={m.paymentType} hover>
                    <TableCell>
                      <Stack spacing={0.25}>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {m.milestoneName}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          Mã đợt: {m.paymentType}
                        </Typography>
                      </Stack>
                    </TableCell>
                    <TableCell>{dateVi(m.dueDate)}</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 600 }}>
                      {money(m.amount)}
                    </TableCell>
                    <TableCell align="right">
                      {m.paidAmount > 0 ? (
                        <Typography variant="body2" color="success.main" sx={{ fontWeight: 600 }}>
                          {money(m.paidAmount)}
                        </Typography>
                      ) : (
                        "—"
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusChip kind="payment" status={m.status} />
                    </TableCell>
                    <TableCell>{m.paymentMethod || "—"}</TableCell>
                    <TableCell>{dateVi(m.paidDate)}</TableCell>
                    <TableCell align="center">
                      {m.receiptAvailable ? (
                        <Button
                          variant="outlined"
                          size="small"
                          startIcon={isDownloading ? <CircularProgress size={14} color="inherit" /> : <DownloadIcon />}
                          disabled={isDownloading}
                          onClick={() => onDownloadReceipt(m.paymentType)}
                        >
                          Tải biên nhận
                        </Button>
                      ) : (
                        <Typography variant="caption" color="text.secondary">
                          {m.status === "Paid" ? "Chưa tải lên" : "Chưa phát hành"}
                        </Typography>
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
