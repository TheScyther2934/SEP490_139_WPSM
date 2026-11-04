import { combineReducers, configureStore } from "@reduxjs/toolkit";
import { configureAuth } from "../api/axiosClient";
import auth, { logout, refreshSession, sessionExpired } from "./authSlice";
import notify from "./notifySlice";
import project from "./projectSlice";
import customer from "./customerSlice";

const appReducer = combineReducers({ auth, notify, project, customer });

// Signing out or losing the session clears every cached project so the next user starts clean.
const rootReducer = (state, action) =>
  appReducer(action.type === logout.fulfilled.type || action.type === sessionExpired.type ? { auth: state?.auth } : state, action);

export const store = configureStore({ reducer: rootReducer });

configureAuth({
  getAccessToken: () => store.getState().auth.accessToken,
  refresh: async () => {
    const result = await store.dispatch(refreshSession());
    return refreshSession.fulfilled.match(result) ? result.payload.accessToken : null;
  },
  expired: () => store.dispatch(sessionExpired()),
});
