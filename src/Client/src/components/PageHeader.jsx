import { Box, Chip, Stack, Typography } from "@mui/material";

// Title row of every screen; the screen code chip keeps the link to the SRS visible.
export default function PageHeader({ code, title, subtitle, actions }) {
  return (
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 3, justifyContent: "space-between", alignItems: { sm: "flex-end" } }}>
      <Box>
        {code && <Chip label={`Mã: ${code}`} size="small" variant="outlined" sx={{ mb: 1 }} />}
        <Typography variant="h5" component="h1" color="primary">{title}</Typography>
        {subtitle && <Typography color="text.secondary" sx={{ mt: 0.5 }}>{subtitle}</Typography>}
      </Box>
      {actions && <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>{actions}</Stack>}
    </Stack>
  );
}
