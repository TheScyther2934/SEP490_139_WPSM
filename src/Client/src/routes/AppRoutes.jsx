import { Navigate, Route, Routes } from "react-router-dom";
import { useSelector } from "react-redux";
import RequireRole from "./RequireRole";
import { PATHS, ROLES, homeFor } from "./paths";
import { selectAuth } from "../store/authSlice";
import AuthLayout from "../layouts/AuthLayout";
import AdminLayout from "../layouts/AdminLayout";
import ManagerLayout from "../layouts/ManagerLayout";
import CustomerLayout from "../layouts/CustomerLayout";
import ProjectLayout from "../layouts/ProjectLayout";
import RoleLayout from "../layouts/RoleLayout";

import Login from "../pages/auth/Login";
import Profile from "../pages/auth/Profile";
import ChangePassword from "../pages/auth/ChangePassword";
import Notifications from "../pages/common/Notifications";
import Forbidden from "../pages/errors/Forbidden";
import NotFound from "../pages/errors/NotFound";

import AdminDashboard from "../pages/admin/Dashboard";
import Catalog from "../pages/admin/Catalog";
import PriceFrames from "../pages/admin/PriceFrames";
import Policies from "../pages/admin/Policies";
import Reports from "../pages/admin/Reports";
import HallUtilization from "../pages/admin/HallUtilization";
import NetRevenue from "../pages/admin/NetRevenue";
import CrStatistics from "../pages/admin/CrStatistics";
import PhotoFeed from "../pages/admin/PhotoFeed";
import PhotoAudit from "../pages/admin/PhotoAudit";
import AdminOverdueAlerts from "../pages/admin/OverdueAlerts";
import EscalatedComplaints from "../pages/admin/EscalatedComplaints";
import ExceptionRequests from "../pages/admin/ExceptionRequests";

import ManagerDashboard from "../pages/manager/Dashboard";
import Projects from "../pages/manager/Projects";
import CreateProject from "../pages/manager/CreateProject";
import ProjectHub from "../pages/manager/ProjectHub";
import Specification from "../pages/manager/Specification";
import ManagerRoadmap from "../pages/manager/Roadmap";
import ManagerChangeRequests from "../pages/manager/ChangeRequests";
import ManagerChangeRequestDetail from "../pages/manager/ChangeRequestDetail";
import CostImpact from "../pages/manager/CostImpact";
import Beo from "../pages/manager/Beo";
import DepartmentSheets from "../pages/manager/DepartmentSheets";
import ManagerPayments from "../pages/manager/Payments";
import SettlementReceipt from "../pages/manager/SettlementReceipt";
import DayOfBoard from "../pages/manager/DayOfBoard";
import ManagerComplaints from "../pages/manager/Complaints";
import ComplaintDetail from "../pages/manager/ComplaintDetail";
import ResourceCalendar from "../pages/manager/ResourceCalendar";
import ConflictDetail from "../pages/manager/ConflictDetail";
import ManagerOverdueAlerts from "../pages/manager/OverdueAlerts";

import Overview from "../pages/customer/Overview";
import Contract from "../pages/customer/Contract";
import Addenda from "../pages/customer/Addenda";
import CustomerRoadmap from "../pages/customer/Roadmap";
import CustomerChangeRequests from "../pages/customer/ChangeRequests";
import CreateChangeRequest from "../pages/customer/CreateChangeRequest";
import CustomerChangeRequestDetail from "../pages/customer/ChangeRequestDetail";
import CustomerPayments from "../pages/customer/Payments";
import LivePhotos from "../pages/customer/LivePhotos";
import PostEvent from "../pages/customer/PostEvent";
import Ratings from "../pages/customer/Ratings";
import CustomerComplaints from "../pages/customer/Complaints";
import CreateComplaint from "../pages/customer/CreateComplaint";
import ComplaintStatus from "../pages/customer/ComplaintStatus";

function Home() {
  const { status, user } = useSelector(selectAuth);
  return <Navigate to={status === "authenticated" ? homeFor(user.role) : PATHS.login} replace />;
}

// One branch per role: each has its own layout and is guarded by RequireRole.
// Dialogs (A-03, A-05, A-07, A-08, A-17, C-04, C-08, M-12, M-14, M-17, M-20) open on their parent page and have no URL.
export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Home />} />

      <Route element={<AuthLayout />}>
        <Route path="/login" element={<Login />} />
        <Route path="/change-password" element={<RequireRole allowPasswordChange><ChangePassword /></RequireRole>} />
      </Route>

      <Route element={<RequireRole><RoleLayout /></RequireRole>}>
        <Route path="/profile" element={<Profile />} />
        <Route path="/notifications" element={<RequireRole roles={[ROLES.MANAGER, ROLES.CUSTOMER]}><Notifications /></RequireRole>} />
        <Route path="/403" element={<Forbidden />} />
      </Route>

      <Route path="/admin" element={<RequireRole roles={[ROLES.ADMIN]}><AdminLayout /></RequireRole>}>
        <Route index element={<Navigate to="dashboard" replace />} />
        <Route path="dashboard" element={<AdminDashboard />} />
        <Route path="catalog" element={<Catalog />} />
        <Route path="price-frames" element={<PriceFrames />} />
        <Route path="policies" element={<Policies />} />
        <Route path="reports">
          <Route index element={<Reports />} />
          <Route path="hall-utilization" element={<HallUtilization />} />
          <Route path="net-revenue" element={<NetRevenue />} />
          <Route path="change-requests" element={<CrStatistics />} />
        </Route>
        <Route path="photo-feed" element={<PhotoFeed />} />
        <Route path="photo-feed/:photoId" element={<PhotoAudit />} />
        <Route path="overdue-alerts" element={<AdminOverdueAlerts />} />
        <Route path="complaints" element={<EscalatedComplaints />} />
        <Route path="exception-requests" element={<ExceptionRequests />} />
        <Route path="*" element={<NotFound />} />
      </Route>

      <Route path="/manager" element={<RequireRole roles={[ROLES.MANAGER]}><ManagerLayout /></RequireRole>}>
        <Route index element={<Navigate to="dashboard" replace />} />
        <Route path="dashboard" element={<ManagerDashboard />} />
        <Route path="projects" element={<Projects />} />
        <Route path="projects/new" element={<CreateProject />} />
        <Route path="projects/:projectCode" element={<ProjectLayout />}>
          <Route index element={<ProjectHub />} />
          <Route path="specification" element={<Specification />} />
          <Route path="roadmap" element={<ManagerRoadmap />} />
          <Route path="change-requests" element={<ManagerChangeRequests />} />
          <Route path="change-requests/:crCode" element={<ManagerChangeRequestDetail />} />
          <Route path="change-requests/:crCode/cost-impact" element={<CostImpact />} />
          <Route path="beo" element={<Beo />} />
          <Route path="beo/department-sheets" element={<DepartmentSheets />} />
          <Route path="payments" element={<ManagerPayments />} />
          <Route path="payments/settlement-receipt" element={<SettlementReceipt />} />
          <Route path="day-of" element={<DayOfBoard />} />
          <Route path="complaints" element={<ManagerComplaints />} />
          <Route path="complaints/:complaintCode" element={<ComplaintDetail />} />
        </Route>
        <Route path="resource-calendar" element={<ResourceCalendar />} />
        <Route path="resource-calendar/conflicts/:allocationId" element={<ConflictDetail />} />
        <Route path="overdue-alerts" element={<ManagerOverdueAlerts />} />
        <Route path="*" element={<NotFound />} />
      </Route>

      <Route path="/customer" element={<RequireRole roles={[ROLES.CUSTOMER]}><CustomerLayout /></RequireRole>}>
        <Route index element={<Navigate to="overview" replace />} />
        <Route path="overview" element={<Overview />} />
        <Route path="contract" element={<Contract />} />
        <Route path="contract/addenda" element={<Addenda />} />
        <Route path="roadmap" element={<CustomerRoadmap />} />
        <Route path="change-requests" element={<CustomerChangeRequests />} />
        <Route path="change-requests/new" element={<CreateChangeRequest />} />
        <Route path="change-requests/:crCode" element={<CustomerChangeRequestDetail />} />
        <Route path="payments" element={<CustomerPayments />} />
        <Route path="live-photos" element={<LivePhotos />} />
        <Route path="post-event" element={<PostEvent />} />
        <Route path="post-event/ratings" element={<Ratings />} />
        <Route path="complaints" element={<CustomerComplaints />} />
        <Route path="complaints/new" element={<CreateComplaint />} />
        <Route path="complaints/:complaintCode" element={<ComplaintStatus />} />
        <Route path="*" element={<NotFound />} />
      </Route>

      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}
