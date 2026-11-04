import { useSelector } from "react-redux";
import { selectUser } from "../store/authSlice";
import { ROLES } from "../routes/paths";
import AdminLayout from "./AdminLayout";
import ManagerLayout from "./ManagerLayout";
import CustomerLayout from "./CustomerLayout";

// Shared pages (/profile, /notifications) keep the navigation of the signed-in role.
export default function RoleLayout() {
  const role = useSelector(selectUser)?.role;
  if (role === ROLES.ADMIN) return <AdminLayout />;
  if (role === ROLES.MANAGER) return <ManagerLayout />;
  return <CustomerLayout />;
}
