import { Link as RouterLink, useParams } from "react-router-dom";
import {
  Box,
  Button,
  Chip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
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
import { money, signedMoney } from "../../../utils/format";

// M-07: Manager Change Requests List (Entry to M-08 / M-09)
export default function ManagerChangeRequests() {
  const { projectCode } = useParams();
  const { data: crs, loading, error, reload } = useAsync(
    () => projectService.getChangeRequests(projectCode),
    [projectCode]
  );

  return (
    <>
      <PageHeader
        code="M-07"
        title={`Danh sách Yêu cầu Thay đổi (${projectCode})`}
        subtitle="Quản lý và thẩm định các yêu cầu thay đổi từ khách hàng, tính toán tác động chi phí và phát hành phụ lục (M-09)."
        actions={
          <Button
            startIcon={<ArrowBackIcon />}
            component={RouterLink}
            to={PATHS.manager.project(projectCode)}
            sx={{ whiteSpace: "nowrap" }}
          >
            Về Project Hub
          </Button>
        }
      />

      <DataState
        loading={loading}
        error={error}
        onRetry={reload}
        empty={!crs?.length}
        emptyText="Dự án chưa có yêu cầu thay đổi (CR) nào."
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
                  <TableCell sx={{ whiteSpace: "nowrap" }}>KH duyệt</TableCell>
                  <TableCell sx={{ whiteSpace: "nowrap" }}>Trạng thái</TableCell>
                  <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>Thao tác</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {crs?.map((item) => (
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
                    <TableCell align="right" sx={{ fontWeight: 800, whiteSpace: "nowrap", color: "primary.main" }}>
                      {signedMoney(item.totalVariance)}
                    </TableCell>
                    <TableCell sx={{ whiteSpace: "nowrap" }}>
                      {item.customerApprovedAt ? (
                        <Chip size="small" color="success" label="Đã duyệt" variant="outlined" />
                      ) : (
                        <Chip size="small" color="default" label="Chưa duyệt" variant="outlined" />
                      )}
                    </TableCell>
                    <TableCell sx={{ whiteSpace: "nowrap" }}>
                      <StatusChip kind="cr" status={item.status} />
                    </TableCell>
                    <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                      <Button
                        size="small"
                        variant={item.status === "Negotiating" ? "contained" : "outlined"}
                        startIcon={<CalculateOutlinedIcon />}
                        component={RouterLink}
                        to={PATHS.manager.costImpact(projectCode, item.crCode)}
                        sx={{ whiteSpace: "nowrap" }}
                      >
                        Tác động chi phí (M-09)
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
