import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";

export default function FinalSettlementReceiptSection({ finalReceipt, onDownload, downloading }) {
  if (!finalReceipt) return null;

  return (
    <Card sx={{ mt: 3, mb: 2 }}>
      <CardContent>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} justifyContent="space-between" alignItems={{ sm: "center" }}>
          <Box>
            <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
              <ReceiptLongIcon color="primary" />
              <Typography variant="h6" component="h3">
                Biên nhận quyết toán hợp đồng
              </Typography>
            </Stack>
            <Typography variant="body2" color="text.secondary">
              Tài liệu xác nhận quyết toán tài chính cuối cùng giữa khách hàng và Ban quản lý tiệc cưới.
            </Typography>
          </Box>

          {finalReceipt.isAvailable && (
            <Button
              variant="contained"
              color="primary"
              startIcon={downloading ? <CircularProgress size={16} color="inherit" /> : <DownloadIcon />}
              disabled={downloading}
              onClick={onDownload}
              sx={{ whiteSpace: "nowrap", alignSelf: { xs: "stretch", sm: "auto" } }}
            >
              Tải biên nhận quyết toán
            </Button>
          )}
        </Stack>

        <Box sx={{ mt: 2 }}>
          {finalReceipt.isAvailable ? (
            <Alert severity="success" variant="outlined">
              {finalReceipt.message || "Biên nhận quyết toán chính thức đã sẵn sàng để tải về."}
            </Alert>
          ) : (
            <Alert severity="info" variant="outlined">
              {finalReceipt.message ||
                "Biên nhận quyết toán sẽ được Ban điều phối phát hành sau khi tiệc cưới hoàn tất và dự án được xác nhận quyết toán (Settled)."}
            </Alert>
          )}
        </Box>
      </CardContent>
    </Card>
  );
}
