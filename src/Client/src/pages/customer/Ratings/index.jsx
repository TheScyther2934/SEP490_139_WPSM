import { useEffect, useState } from "react";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Divider,
  Grid,
  Rating as MuiRating,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import SendOutlinedIcon from "@mui/icons-material/SendOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, errorMessage } from "../../../utils/format";

// C-13: Rate Services (Hall, Menu, Decor, MC) (UC-14 | GBR-10 | MSG16/26)
export default function Ratings() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { data, loading, error, reload, setData } = useAsync(() => customerService.getPostEvent(), []);
  const [form, setForm] = useState({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (data?.ratings) {
      const next = {};
      data.ratings.forEach((r) => {
        next[r.module] = {
          stars: r.stars || 5,
          comment: r.comment || "",
        };
      });
      setForm(next);
    }
  }, [data]);

  const handleFieldChange = (module, field, value) => {
    setForm((prev) => ({
      ...prev,
      [module]: {
        ...(prev[module] || { stars: 5, comment: "" }),
        [field]: value,
      },
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!data) return;

    const payload = data.ratings.map((r) => ({
      module: r.module,
      stars: Number(form[r.module]?.stars || 0),
      comment: (form[r.module]?.comment || "").trim(),
    }));

    if (payload.some((p) => p.stars < 1 || p.stars > 5)) {
      dispatch(notify({ message: "Vui lòng chọn từ 1 đến 5 sao cho cả 4 hạng mục dịch vụ (MSG16).", severity: "warning" }));
      return;
    }

    setSubmitting(true);
    try {
      const updated = await customerService.submitRatings(data.projectCode, payload);
      setData(updated);
      dispatch(
        notify({
          message: "Đã ghi nhận đánh giá dịch vụ thành công. Đánh giá đã được khóa theo quy tắc GBR-10 (MSG26).",
          severity: "success",
        })
      );
      navigate(PATHS.customer.postEvent);
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader
        code="C-13"
        title="Đánh giá Chất lượng Dịch vụ Tiệc cưới"
        subtitle="Chấm điểm từ 1–5 sao và nhận xét cho 4 nhóm dịch vụ: Sảnh tiệc (Hall), Thực đơn (Menu), Trang trí (Decor) và MC."
        actions={
          <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.postEvent}>
            Về tổng kết sau tiệc (C-12)
          </Button>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload} empty={!data} emptyText="Không tìm thấy dự án để đánh giá.">
        {data && (
          <Box component="form" onSubmit={handleSubmit}>
            <Stack spacing={3}>
              {data.isLocked ? (
                <Alert severity="warning" icon={<LockOutlinedIcon />}>
                  <strong>MSG26 / GBR-10:</strong> Bạn đã gửi đánh giá cho dự án <strong>{data.projectCode}</strong>. Kết quả đánh giá là bất biến và không thể chỉnh sửa lại.
                </Alert>
              ) : (
                <Alert severity="info">
                  Vui lòng chọn số sao (1–5 sao) cho từng hạng mục dưới đây. Sau khi nhấn <strong>Gửi đánh giá</strong>, kết quả sẽ được lưu cố định và không thể thay đổi (GBR-10).
                </Alert>
              )}

              <Grid container spacing={2.5}>
                {data.ratings?.map((item) => {
                  const current = form[item.module] || { stars: item.stars || 5, comment: item.comment || "" };
                  return (
                    <Grid item xs={12} md={6} key={item.module}>
                      <Card variant="outlined" sx={{ height: "100%" }}>
                        <CardContent>
                          <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                            <Box>
                              <Typography variant="subtitle1" fontWeight={800}>
                                {item.moduleLabel}
                              </Typography>
                              <Typography variant="caption" color="text.secondary">
                                Mã phân hệ: {item.module}
                              </Typography>
                            </Box>
                            <MuiRating
                              name={`rating-${item.module}`}
                              value={Number(current.stars) || 0}
                              readOnly={data.isLocked}
                              onChange={(_, val) => handleFieldChange(item.module, "stars", val || 1)}
                              size="large"
                            />
                          </Stack>
                          <Divider sx={{ mb: 2 }} />
                          <TextField
                            label="Nhận xét chi tiết (tối đa 500 ký tự)"
                            multiline
                            minRows={3}
                            fullWidth
                            disabled={data.isLocked}
                            value={current.comment}
                            onChange={(e) => handleFieldChange(item.module, "comment", e.target.value)}
                            inputProps={{ maxLength: 500 }}
                            placeholder={`Chia sẻ trải nghiệm của gia đình về ${item.moduleLabel.toLowerCase()}...`}
                          />
                          {item.ratedAt && (
                            <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 1 }}>
                              Đã khóa lúc: {dateTimeVi(item.ratedAt)}
                            </Typography>
                          )}
                        </CardContent>
                      </Card>
                    </Grid>
                  );
                })}
              </Grid>

              {!data.isLocked && (
                <Stack direction="row" justifyContent="flex-end" spacing={1.5}>
                  <Button component={RouterLink} to={PATHS.customer.postEvent} disabled={submitting}>
                    Hủy
                  </Button>
                  <Button
                    type="submit"
                    variant="contained"
                    size="large"
                    startIcon={<SendOutlinedIcon />}
                    disabled={submitting}
                    id="submitRatingsBtn"
                  >
                    {submitting ? "Đang gửi đánh giá..." : "Gửi đánh giá & Khóa kết quả"}
                  </Button>
                </Stack>
              )}
            </Stack>
          </Box>
        )}
      </DataState>
    </>
  );
}
