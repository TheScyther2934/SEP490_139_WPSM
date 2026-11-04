import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Box, CircularProgress } from "@mui/material";
import AppRoutes from "./routes/AppRoutes";
import AppSnackbar from "./components/AppSnackbar";
import { refreshSession, selectAuth } from "./store/authSlice";

// The access token is not kept across reloads; ask the refresh cookie for a new one before routing.
export default function App() {
  const dispatch = useDispatch();
  const { status } = useSelector(selectAuth);
  useEffect(() => { if (status === "unknown") dispatch(refreshSession()); }, [status, dispatch]);

  if (status === "unknown")
    return (
      <Box sx={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center" }}>
        <CircularProgress aria-label="Đang khôi phục phiên đăng nhập" />
      </Box>
    );
  return (
    <>
      <AppRoutes />
      <AppSnackbar />
    </>
  );
}
