import { Navigate, useLocation } from "react-router-dom";
import { useSelector } from "react-redux";
import { selectAuth } from "../store/authSlice";
import { PATHS } from "./paths";

// UX guard only: the API checks the role again on every call.
// No `roles` means any signed-in user. A temporary password must be changed before anything else (G-03).
export default function RequireRole({ roles, allowPasswordChange = false, children }) {
  const { status, user } = useSelector(selectAuth);
  const location = useLocation();

  if (status !== "authenticated") return <Navigate to={PATHS.login} replace state={{ from: location }} />;
  if (user.mustChangePassword && !allowPasswordChange) return <Navigate to={PATHS.changePassword} replace />;
  if (roles && !roles.includes(user.role)) return <Navigate to={PATHS.forbidden} replace />;
  return children;
}
