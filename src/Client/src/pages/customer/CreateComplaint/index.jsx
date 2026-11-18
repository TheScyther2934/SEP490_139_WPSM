import { useEffect, useState } from "react";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import SendOutlinedIcon from "@mui/icons-material/SendOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, errorMessage } from "../../../utils/format";

// C-15: File a Service Complaint against BEO commitment (UC-15 | MSG25)
export default function CreateComplaint() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { data: beoOptions, loading, error, reload } = useAsync(() => customerService.getBeoOptions(), []);

  const [beoId, setBeoId] = useState("");
  const [description, setDescription] = useState("");
  const [validationError, setValidationError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (beoOptions?.length && !beoId) {
      setBeoId(beoOptions[0].beoId);
    }
  }, [beoOptions, beoId]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const trimmed = description.trim();
    if (!trimmed) {
      setValidationError("Vui lòng nhập mô tả chi tiết nội dung khiếu nại (MSG25).");
      return;
    }
    setValidationError("");
    setSubmitting(true);
    try {
      const created = await customerService.createComplaint({
        beoId: beoId ? Number(beoId) : null,
        description: trimmed,
      });
      dispatch(
        notify({
          message: `Đã gửi hồ sơ khiếu nại ${created.complaintCode} thành công.`,
          severity: "success",
        })
      );
      navigate(PATHS.customer.complaint(created.complaintCode));
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader
        code="C-15"
        title="Gửi Hồ sơ Khiếu nại Dịch vụ"
        subtitle="Chọn phiên bản BEO cam kết để đối chiếu và mô tả chi tiết sự việc cần Quản lý dự án giải quyết."
        actions={
          <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.complaints}>
            Danh sách khiếu nại (C-14)
          </Button>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload}>
        <Card variant="outlined" sx={{ maxWidth: 760 }}>
          <CardContent>
            <Box component="form" onSubmit={handleSubmit}>
              <Stack spacing={2.5}>
                <Alert severity="info">
                  Mọi khiếu nại sẽ được gắn mã hồ sơ riêng và đối chiếu trực tiếp với <strong>Hồ sơ vận hành BEO</strong> cùng ảnh nghiệm thu thực tế tại sảnh.
                </Alert>

                <FormControl fullWidth>
                  <InputLabel id="beo-select-label">Phiên bản BEO đối chiếu cam kết</InputLabel>
                  <Select
                    labelId="beo-select-label"
                    label="Phiên bản BEO đối chiếu cam kết"
                    value={beoId}
                    onChange={(e) => setBeoId(e.target.value)}
                  >
                    {beoOptions?.length ? (
                      beoOptions.map((b) => (
                        <MenuItem key={b.beoId} value={b.beoId}>
                          BEO v{b.version} ({b.status}) {b.frozenAt ? `· Chốt ngày ${dateTimeVi(b.frozenAt)}` : ""}
                        </MenuItem>
                      ))
                    ) : (
                      <MenuItem value="">BEO mặc định của dự án</MenuItem>
                    )}
                  </Select>
                </FormControl>

                <TextField
                  label="Nội dung phản ánh / khiếu nại chi tiết *"
                  multiline
                  minRows={5}
                  value={description}
                  onChange={(e) => {
                    setDescription(e.target.value);
                    if (validationError) setValidationError("");
                  }}
                  error={Boolean(validationError)}
                  helperText={
                    validationError ||
                    `${description.length}/1000 ký tự · Nêu rõ hạng mục (Sảnh, Thực đơn, Trang trí, Âm thanh/MC) và sai lệch so với cam kết.`
                  }
                  inputProps={{ maxLength: 1000 }}
                  placeholder="Ví dụ: Số lượng bàn tiệc hoặc hoa tươi khu vực sân khấu thực tế không đúng với cam kết trong BEO..."
                  fullWidth
                />

                <Stack direction="row" justifyContent="flex-end" spacing={1.5}>
                  <Button component={RouterLink} to={PATHS.customer.complaints} disabled={submitting}>
                    Hủy
                  </Button>
                  <Button
                    type="submit"
                    variant="contained"
                    startIcon={<SendOutlinedIcon />}
                    disabled={submitting}
                    id="submitComplaintBtn"
                  >
                    {submitting ? "Đang gửi hồ sơ..." : "Gửi khiếu nại"}
                  </Button>
                </Stack>
              </Stack>
            </Box>
          </CardContent>
        </Card>
      </DataState>
    </>
  );
}
