import ReportLayout from "../../../components/reports/ReportLayout";
import NetRevenueReport from "./components/NetRevenueReport";

// A-11
export default function NetRevenue() {
  return <ReportLayout code="A-11" title="Doanh thu thuần" render={(report) => <NetRevenueReport report={report} />} />;
}
