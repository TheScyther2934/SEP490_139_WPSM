import { Box, Stack, Typography } from "@mui/material";
import { useTheme } from "@mui/material/styles";

// Title row of every screen. Screen codes stay in the source only (`code` is accepted but never shown to users).
export default function PageHeader({ title, subtitle, actions }) {
  const plain = useTheme().wpsm?.plainHeader;
  return (
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 3, justifyContent: "space-between", alignItems: { sm: plain ? "center" : "flex-end" } }}>
      <Box>
        <Typography variant={plain ? "h4" : "h5"} component="h1" color={plain ? "text.primary" : "primary"}>{title}</Typography>
        {subtitle && <Typography color="text.secondary" sx={{ mt: 0.5 }}>{subtitle}</Typography>}
      </Box>
      {actions && <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>{actions}</Stack>}
    </Stack>
  );
}
