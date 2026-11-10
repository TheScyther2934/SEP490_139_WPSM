import { Link as RouterLink } from "react-router-dom";
import {
  Button,
  Chip,
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
import AddIcon from "@mui/icons-material/Add";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi } from "../../../utils/format";

// C-14: Customer Complaints List (UC-15, UC-16)
export default function CustomerComplaints() {
  const { data, loading, error, reload } = useAsync(() => customerService.getComplaints(), []);

  return (
    <>
      <PageHeader
        code="C-14"
        title="Danh sách Khiếu nại Dịch vụ"
        subtitle="Theo dõi các hồ sơ khiếu nại đối chiếu với BEO cam kết và kết quả giải quyết từ Quản lý / Ban Quản trị."
        actions={
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            component={RouterLink}
            to={PATHS.customer.newComplaint}
            id="newComplaintBtn"
          >
            Gửi khiếu nại mới (C-15)
          </Button>
        }
      />

      <DataState
        loading={loading}
        error={error}
        onRetry={reload}
        empty={!data?.length}
        emptyText="Gia đình chưa gửi hồ sơ khiếu nại nào."
      >
        <Paper variant="outlined">
          <TableContainer>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>Mã khiếu nại</TableCell>
                  <TableCell>BEO đối chiếu</TableCell>
                  <TableCell>Nội dung phản ánh</TableCell>
                  <TableCell>Ngày gửi</TableCell>
                  <TableCell>Trạng thái</TableCell>
                  <TableCell align="right">Thao tác</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data?.map((item) => (
                  <TableRow key={item.complaintCode} hover>
                    <TableCell sx={{ fontWeight: 800 }}>{item.complaintCode}</TableCell>
                    <TableCell>
                      <Chip size="small" label={`BEO v${item.beoVersion}`} variant="outlined" />
                    </TableCell>
                    <TableCell sx={{ maxWidth: 420 }}>
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
                        startIcon={<VisibilityOutlinedIcon />}
                        component={RouterLink}
                        to={PATHS.customer.complaint(item.complaintCode)}
                      >
                        Chi tiết (C-16)
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
