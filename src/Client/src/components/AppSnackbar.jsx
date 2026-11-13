import { Alert, Snackbar } from "@mui/material";
import { useDispatch, useSelector } from "react-redux";
import { closeNotify } from "../store/notifySlice";

export default function AppSnackbar() {
  const dispatch = useDispatch();
  const { key, message, severity, open } = useSelector((s) => s.notify);
  const close = () => dispatch(closeNotify());
  return (
    <Snackbar key={key} open={open} autoHideDuration={5000} onClose={close} anchorOrigin={{ vertical: "bottom", horizontal: "right" }}>
      <Alert severity={severity} variant="filled" onClose={close} sx={{ width: "100%" }}>{message}</Alert>
    </Snackbar>
  );
}
