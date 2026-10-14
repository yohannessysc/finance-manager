
export interface AuthUser {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}
