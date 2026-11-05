import { Outlet } from "react-router-dom";
import { Box, Stack, Typography } from "@mui/material";
import DiamondIcon from "@mui/icons-material/Diamond";

// Centred card frame for sign-in and the forced password change.
export default function AuthLayout() {
  return (
    <Box sx={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", p: 2,
      background: "linear-gradient(135deg, #fff8f5 0%, #ffd9df 100%)" }}>
      <Box sx={{ width: "100%", maxWidth: 440 }}>
        <Stack direction="row" spacing={1} sx={{ justifyContent: "center", alignItems: "center", mb: 3 }}>
          <DiamondIcon color="primary" fontSize="large" />
          <Typography variant="h5" color="primary">WPSM</Typography>
        </Stack>
        <Outlet />
      </Box>
    </Box>
  );
}
