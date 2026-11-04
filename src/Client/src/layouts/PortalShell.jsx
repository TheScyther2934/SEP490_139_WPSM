import { useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useDispatch, useSelector } from "react-redux";
import {
  AppBar, Avatar, Box, Divider, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText,
  ListSubheader, Menu, MenuItem, Toolbar, Typography,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import DiamondIcon from "@mui/icons-material/Diamond";
import { logout, selectUser } from "../store/authSlice";
import { PATHS } from "../routes/paths";
import { initials, roleLabel } from "../utils/format";

const DRAWER_WIDTH = 264;

// Shared frame of the three role layouts: side navigation, top bar with the account menu, and the page outlet.
// `sections` = [{ title?, items: [{ label, icon, to, end? }] }].
export default function PortalShell({ brand, sections, header }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [menuAnchor, setMenuAnchor] = useState(null);
  const user = useSelector(selectUser);
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const signOut = async () => {
    setMenuAnchor(null);
    await dispatch(logout());
    navigate(PATHS.login, { replace: true });
  };

  const drawer = (
    <Box sx={{ height: "100%", display: "flex", flexDirection: "column" }}>
      <Toolbar sx={{ gap: 1 }}>
        <DiamondIcon color="primary" />
        <Typography variant="h6" color="primary" noWrap>{brand}</Typography>
      </Toolbar>
      <Divider />
      <Box component="nav" aria-label="Điều hướng chính" sx={{ flex: 1, overflowY: "auto" }}>
        {sections.map((section, index) => (
          <List key={section.title ?? index} subheader={section.title && <ListSubheader sx={{ bgcolor: "transparent" }}>{section.title}</ListSubheader>}>
            {section.items.map(({ label, icon: Icon, to, end }) => (
              <ListItemButton
                key={to}
                component={NavLink}
                to={to}
                end={end}
                onClick={() => setMobileOpen(false)}
                sx={{ mx: 1, borderRadius: 2, "&.active": { bgcolor: "primary.main", color: "primary.contrastText", "& .MuiListItemIcon-root": { color: "inherit" } } }}
              >
                <ListItemIcon sx={{ minWidth: 40 }}><Icon fontSize="small" /></ListItemIcon>
                <ListItemText primary={label} />
              </ListItemButton>
            ))}
          </List>
        ))}
      </Box>
    </Box>
  );

  return (
    <Box sx={{ display: "flex", minHeight: "100vh" }}>
      <AppBar position="fixed" color="inherit" elevation={0} sx={{ borderBottom: 1, borderColor: "divider", width: { md: `calc(100% - ${DRAWER_WIDTH}px)` }, ml: { md: `${DRAWER_WIDTH}px` } }}>
        <Toolbar sx={{ gap: 1 }}>
          <IconButton edge="start" aria-label="Mở menu" onClick={() => setMobileOpen(true)} sx={{ display: { md: "none" } }}>
            <MenuIcon />
          </IconButton>
          <Box sx={{ flex: 1, minWidth: 0 }}>{header}</Box>
          <IconButton aria-label="Tài khoản" onClick={(e) => setMenuAnchor(e.currentTarget)}>
            <Avatar sx={{ bgcolor: "primary.main", width: 36, height: 36, fontSize: 15 }}>{initials(user?.fullName)}</Avatar>
          </IconButton>
          <Menu anchorEl={menuAnchor} open={!!menuAnchor} onClose={() => setMenuAnchor(null)}>
            <Box sx={{ px: 2, py: 1 }}>
              <Typography fontWeight={600}>{user?.fullName}</Typography>
              <Typography variant="body2" color="text.secondary">{roleLabel(user?.role)}</Typography>
            </Box>
            <Divider />
            <MenuItem onClick={() => { setMenuAnchor(null); navigate(PATHS.profile); }}>Hồ sơ cá nhân</MenuItem>
            <MenuItem onClick={signOut} sx={{ color: "error.main" }}>Đăng xuất</MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Box sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        <Drawer variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)} ModalProps={{ keepMounted: true }}
          sx={{ display: { xs: "block", md: "none" }, "& .MuiDrawer-paper": { width: DRAWER_WIDTH } }}>
          {drawer}
        </Drawer>
        <Drawer variant="permanent" open sx={{ display: { xs: "none", md: "block" }, "& .MuiDrawer-paper": { width: DRAWER_WIDTH, bgcolor: "#faf2ee" } }}>
          {drawer}
        </Drawer>
      </Box>

      <Box component="main" sx={{ flex: 1, minWidth: 0, p: { xs: 2, md: 4 } }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  );
}
