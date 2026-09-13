import { createContext } from 'react';
import type { LoginResponse } from '@/api/types';
import type { StoredAuth } from './tokenStorage';

export interface AuthContextValue {
  auth: StoredAuth | null;
  isAuthenticated: boolean;
  signIn: (login: LoginResponse) => void;
  signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
