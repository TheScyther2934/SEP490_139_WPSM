import { Link as RouterLink } from "react-router-dom";
import { useSelector } from "react-redux";
import { Box, Button, Typography } from "@mui/material";
import { selectAuth } from "../../../../store/authSlice";
import { PATHS, homeFor } from "../../../../routes/paths";

export default function ErrorScreen({ code, title, text }) {
  const { status, user } = useSelector(selectAuth);
  const home = status === "authenticated" ? homeFor(user.role) : PATHS.login;
  return (
    <Box sx={{ textAlign: "center", py: 10, px: 2 }}>
      <Typography variant="h2" color="primary" fontWeight={700}>{code}</Typography>
      <Typography variant="h5" sx={{ mt: 1 }}>{title}</Typography>
      <Typography color="text.secondary" sx={{ mt: 1 }}>{text}</Typography>
      <Button component={RouterLink} to={home} variant="contained" sx={{ mt: 3 }}>Về trang chủ</Button>
    </Box>
  );
}
