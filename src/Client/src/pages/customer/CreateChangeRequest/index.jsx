import { useEffect, useMemo, useState } from "react";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Divider,
  FormControl,
  Grid,
  IconButton,
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
  TextField,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import SendOutlinedIcon from "@mui/icons-material/SendOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateVi, errorMessage, money, signedMoney } from "../../../utils/format";

const CR_TYPES = [
  { value: "Table count", label: "Table count · Thay đổi số lượng bàn tiệc" },
  { value: "Menu change", label: "Menu change · Điều chỉnh thực đơn" },
  { value: "Decor change", label: "Decor change · Nâng cấp / thay đổi trang trí" },
  { value: "Script edit", label: "Script edit · Điều chỉnh kịch bản lễ & MC" },
  { value: "Other", label: "Other · Yêu cầu điều chỉnh khác" },
];

// C-06: Customer Create Change Request against frozen/active contract specifications (UC-08, GBR-03)
export default function CreateChangeRequest() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { data: ctx, loading, error, reload } = useAsync(() => customerService.getChangeRequestContext(), []);

  const [type, setType] = useState("Table count");
  const [description, setDescription] = useState("");
  const [rows, setRows] = useState([]);
  const [validationError, setValidationError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (ctx?.specItems?.length && rows.length === 0) {
      const first = ctx.specItems[0];
      setRows([
        {
          targetSpecItemId: first.specItemId,
          newQuantity: first.currentQuantity,
          newUnitPrice: first.currentUnitPrice,
        },
      ]);
    }
  }, [ctx, rows.length]);

  const specMap = useMemo(() => {
    const map = new Map();
    ctx?.specItems?.forEach((s) => map.set(s.specItemId, s));
    return map;
  }, [ctx]);

  const handleSelectSpec = (index, specItemId) => {
    const spec = specMap.get(Number(specItemId));
    if (!spec) return;
    setRows((prev) =>
      prev.map((row, idx) =>
        idx === index
          ? {
              targetSpecItemId: spec.specItemId,
              newQuantity: spec.currentQuantity,
              newUnitPrice: spec.currentUnitPrice,
            }
          : row
      )
    );
  };

  const handleChangeRow = (index, field, value) => {
    setRows((prev) =>
      prev.map((row, idx) => (idx === index ? { ...row, [field]: value } : row))
    );
  };

  const handleAddRow = () => {
    if (!ctx?.specItems?.length) return;
    const usedIds = new Set(rows.map((r) => Number(r.targetSpecItemId)));
    const nextSpec = ctx.specItems.find((s) => !usedIds.has(s.specItemId)) ?? ctx.specItems[0];
    setRows((prev) => [
      ...prev,
      {
        targetSpecItemId: nextSpec.specItemId,
        newQuantity: nextSpec.currentQuantity,
        newUnitPrice: nextSpec.currentUnitPrice,
      },
    ]);
  };

  const handleRemoveRow = (index) => {
    setRows((prev) => prev.filter((_, idx) => idx !== index));
  };

  const computedSummary = useMemo(() => {
    let costImpact = 0;
    const lines = rows.map((r) => {
      const spec = specMap.get(Number(r.targetSpecItemId));
      const oldQty = spec?.currentQuantity ?? 0;
      const oldPrice = spec?.currentUnitPrice ?? 0;
      const newQty = Math.max(0, Number(r.newQuantity) || 0);
      const newPrice = Math.max(0, Number(r.newUnitPrice) || 0);
      const delta = newQty * newPrice - oldQty * oldPrice;
      costImpact += delta;
      return { spec, oldQty, oldPrice, newQty, newPrice, delta };
    });

    const penaltyRate = ctx?.penaltyRate ?? 0;
    const penaltyFee = Math.round((Math.abs(costImpact) * penaltyRate) / 100);
    const totalVariance = costImpact + penaltyFee;
    const projectedTotal = Math.max(0, (ctx?.currentProjectTotal ?? 0) + totalVariance);

    return { lines, costImpact, penaltyRate, penaltyFee, totalVariance, projectedTotal };
  }, [rows, specMap, ctx]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const trimmedDesc = description.trim();
    if (!trimmedDesc) {
      setValidationError("Vui lòng nhập lý do và mô tả chi tiết yêu cầu thay đổi.");
      return;
    }
    if (!rows.length) {
      setValidationError("Vui lòng chọn ít nhất 1 hạng mục hợp đồng cần điều chỉnh.");
      return;
    }

    const usedIds = new Set();
    for (const r of rows) {
      const id = Number(r.targetSpecItemId);
      if (usedIds.has(id)) {
        setValidationError("Mỗi hạng mục đặc tả chỉ được chọn 1 dòng trong cùng yêu cầu thay đổi.");
        return;
      }
      usedIds.add(id);
    }

    const hasAnyDiff = computedSummary.lines.some(
      (l) => l.newQty !== l.oldQty || l.newPrice !== l.oldPrice
    );
    if (!hasAnyDiff) {
      setValidationError("Số lượng mới hoặc đơn giá mới phải khác với đặc tả hiện tại trong hợp đồng.");
      return;
    }

    setValidationError("");
    setSubmitting(true);
    try {
      const created = await customerService.createChangeRequest({
        projectCode: ctx?.projectCode,
        type,
        description: trimmedDesc,
        items: rows.map((r) => ({
          targetSpecItemId: Number(r.targetSpecItemId),
          newQuantity: Math.max(0, Number(r.newQuantity) || 0),
          newUnitPrice: Math.max(0, Number(r.newUnitPrice) || 0),
        })),
      });
      dispatch(
        notify({
          message: `Đã gửi yêu cầu thay đổi ${created.crCode} thành công.`,
          severity: "success",
        })
      );
      navigate(PATHS.customer.changeRequest(created.crCode));
    } catch (err) {
      setValidationError(errorMessage(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader
        code="C-06"
        title="Tạo Yêu cầu Thay đổi Hợp đồng (Change Request)"
        subtitle="Đề xuất điều chỉnh số lượng bàn, thực đơn, gói trang trí hoặc MC và xem trước tác động chi phí theo quy tắc T-day."
        actions={
          <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.changeRequests}>
            Danh sách yêu cầu (C-05)
          </Button>
        }
      />

      <DataState
        loading={loading}
        error={error}
        onRetry={reload}
        empty={!ctx}
        emptyText="Không tìm thấy thông tin hợp đồng của bạn."
      >
        {ctx && (
          <Box component="form" onSubmit={handleSubmit}>
            <Stack spacing={3}>
              <Alert severity={ctx.penaltyRate > 0 ? "warning" : "info"}>
                Dự án <strong>{ctx.projectCode}</strong> ({ctx.coupleName}) · Sảnh <strong>{ctx.hallName}</strong> · Ngày tiệc:{" "}
                <strong>{dateVi(ctx.eventDate)}</strong> (còn <strong>{ctx.daysUntilEvent}</strong> ngày).{" "}
                {ctx.penaltyRate > 0
                  ? `Theo chính sách GBR-03, yêu cầu thay đổi trong giai đoạn cận ngày áp dụng mức phí điều chỉnh ${ctx.penaltyRate}% trên giá trị chênh lệch.`
                  : "Hiện tại yêu cầu thay đổi nằm ngoài ngưỡng phạt cận ngày (phí điều chỉnh T-day: 0%)."}
              </Alert>

              {validationError && <Alert severity="error">{validationError}</Alert>}

              <Grid container spacing={3}>
                <Grid size={{ xs: 12, lg: 8 }}>
                  <Card variant="outlined">
                    <CardContent>
                      <Stack spacing={2.5}>
                        <Typography variant="h6" fontWeight={700}>
                          1. Thông tin chung & Hạng mục điều chỉnh
                        </Typography>

                        <FormControl fullWidth>
                          <InputLabel id="cr-type-label">Nhóm thay đổi *</InputLabel>
                          <Select
                            labelId="cr-type-label"
                            label="Nhóm thay đổi *"
                            value={type}
                            onChange={(e) => setType(e.target.value)}
                          >
                            {CR_TYPES.map((t) => (
                              <MenuItem key={t.value} value={t.value}>
                                {t.label}
                              </MenuItem>
                            ))}
                          </Select>
                        </FormControl>

                        <Box>
                          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
                            <Typography variant="subtitle1" fontWeight={600}>
                              Chi tiết hạng mục cần điều chỉnh
                            </Typography>
                            <Button
                              size="small"
                              startIcon={<AddIcon />}
                              onClick={handleAddRow}
                              disabled={rows.length >= (ctx.specItems?.length ?? 0)}
                            >
                              Thêm hạng mục
                            </Button>
                          </Stack>

                          <TableContainer component={Paper} variant="outlined">
                            <Table size="small">
                              <TableHead>
                                <TableRow>
                                  <TableCell sx={{ minWidth: 220 }}>Hạng mục trong hợp đồng</TableCell>
                                  <TableCell align="right">SL hiện tại</TableCell>
                                  <TableCell align="right">Đơn giá hiện tại</TableCell>
                                  <TableCell align="right" sx={{ width: 120 }}>SL mới *</TableCell>
                                  <TableCell align="right" sx={{ width: 160 }}>Đơn giá mới (VND) *</TableCell>
                                  <TableCell align="right">Chênh lệch</TableCell>
                                  <TableCell align="center" sx={{ width: 50 }} />
                                </TableRow>
                              </TableHead>
                              <TableBody>
                                {computedSummary.lines.map((line, index) => (
                                  <TableRow key={index}>
                                    <TableCell>
                                      <Select
                                        size="small"
                                        fullWidth
                                        value={rows[index].targetSpecItemId}
                                        onChange={(e) => handleSelectSpec(index, e.target.value)}
                                      >
                                        {ctx.specItems.map((s) => (
                                          <MenuItem key={s.specItemId} value={s.specItemId}>
                                            {s.itemName} ({s.category} · {s.unit})
                                          </MenuItem>
                                        ))}
                                      </Select>
                                    </TableCell>
                                    <TableCell align="right">
                                      {line.oldQty} {line.spec?.unit}
                                    </TableCell>
                                    <TableCell align="right">{money(line.oldPrice)}</TableCell>
                                    <TableCell align="right">
                                      <TextField
                                        size="small"
                                        type="number"
                                        inputProps={{ min: 0 }}
                                        value={rows[index].newQuantity}
                                        onChange={(e) => handleChangeRow(index, "newQuantity", e.target.value)}
                                      />
                                    </TableCell>
                                    <TableCell align="right">
                                      <TextField
                                        size="small"
                                        type="number"
                                        inputProps={{ min: 0, step: 100000 }}
                                        value={rows[index].newUnitPrice}
                                        onChange={(e) => handleChangeRow(index, "newUnitPrice", e.target.value)}
                                      />
                                    </TableCell>
                                    <TableCell
                                      align="right"
                                      sx={{
                                        fontWeight: 600,
                                        color:
                                          line.delta > 0
                                            ? "warning.main"
                                            : line.delta < 0
                                            ? "success.main"
                                            : "text.secondary",
                                      }}
                                    >
                                      {signedMoney(line.delta)}
                                    </TableCell>
                                    <TableCell align="center">
                                      <IconButton
                                        size="small"
                                        color="error"
                                        disabled={rows.length <= 1}
                                        onClick={() => handleRemoveRow(index)}
                                      >
                                        <CloseIcon fontSize="small" />
                                      </IconButton>
                                    </TableCell>
                                  </TableRow>
                                ))}
                              </TableBody>
                            </Table>
                          </TableContainer>
                        </Box>

                        <TextField
                          label="Lý do & Mô tả chi tiết yêu cầu thay đổi *"
                          multiline
                          minRows={4}
                          value={description}
                          onChange={(e) => {
                            setDescription(e.target.value);
                            if (validationError) setValidationError("");
                          }}
                          inputProps={{ maxLength: 500 }}
                          helperText={`${description.length}/500 ký tự · Nêu rõ mong muốn điều chỉnh để Quản lý dự án thẩm định năng lực đáp ứng.`}
                          placeholder="Ví dụ: Gia đình hai bên phát sinh thêm khách mời nên cần tăng thêm 5 bàn tiệc tiêu chuẩn..."
                          fullWidth
                        />
                      </Stack>
                    </CardContent>
                  </Card>
                </Grid>

                <Grid size={{ xs: 12, lg: 4 }}>
                  <Card variant="outlined">
                    <CardContent>
                      <Stack spacing={2}>
                        <Typography variant="h6" fontWeight={700}>
                          2. Tạm tính Tác động Chi phí
                        </Typography>
                        <Divider />

                        <Stack direction="row" justifyContent="space-between">
                          <Typography color="text.secondary">Giá trị hợp đồng gốc:</Typography>
                          <Typography fontWeight={600}>{money(ctx.baseContractValue)}</Typography>
                        </Stack>

                        <Stack direction="row" justifyContent="space-between">
                          <Typography color="text.secondary">Tổng hợp đồng hiện tại:</Typography>
                          <Typography fontWeight={600}>{money(ctx.currentProjectTotal)}</Typography>
                        </Stack>

                        <Divider />

                        <Stack direction="row" justifyContent="space-between">
                          <Typography color="text.secondary">Chênh lệch dịch vụ (Cost Impact):</Typography>
                          <Typography fontWeight={600}>{signedMoney(computedSummary.costImpact)}</Typography>
                        </Stack>

                        <Stack direction="row" justifyContent="space-between">
                          <Typography color="text.secondary">
                            Phí điều chỉnh cận ngày ({computedSummary.penaltyRate}%):
                          </Typography>
                          <Typography fontWeight={600} color={computedSummary.penaltyFee > 0 ? "error.main" : "text.primary"}>
                            {money(computedSummary.penaltyFee)}
                          </Typography>
                        </Stack>

                        <Stack direction="row" justifyContent="space-between">
                          <Typography fontWeight={700}>Tổng biến động (Total Variance):</Typography>
                          <Typography
                            variant="subtitle1"
                            fontWeight={700}
                            color={computedSummary.totalVariance >= 0 ? "primary.main" : "success.main"}
                          >
                            {signedMoney(computedSummary.totalVariance)}
                          </Typography>
                        </Stack>

                        <Divider />

                        <Stack direction="row" justifyContent="space-between">
                          <Typography fontWeight={700}>Tổng hợp đồng sau điều chỉnh:</Typography>
                          <Typography variant="h6" fontWeight={700} color="primary.main">
                            {money(computedSummary.projectedTotal)}
                          </Typography>
                        </Stack>

                        <Stack direction="row" spacing={1.5} sx={{ pt: 1 }}>
                          <Button
                            fullWidth
                            variant="outlined"
                            component={RouterLink}
                            to={PATHS.customer.changeRequests}
                            disabled={submitting}
                          >
                            Hủy
                          </Button>
                          <Button
                            fullWidth
                            type="submit"
                            variant="contained"
                            startIcon={<SendOutlinedIcon />}
                            disabled={submitting}
                          >
                            {submitting ? "Đang gửi..." : "Gửi yêu cầu"}
                          </Button>
                        </Stack>
                      </Stack>
                    </CardContent>
                  </Card>
                </Grid>
              </Grid>
            </Stack>
          </Box>
        )}
      </DataState>
    </>
  );
}
