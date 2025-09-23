# Budget Tracker Web Application

A modern React + TypeScript + Vite application for personal finance tracking with a minimalistic design.

## Environment Configuration

### API Base URL

The application uses the `VITE_API_BASE_URL` environment variable to configure the API endpoint.

**Setting the environment variable:**

**Linux/macOS (bash/zsh):**
```bash
# For development
export VITE_API_BASE_URL=http://localhost:5295/api
npm run dev

# Or inline
VITE_API_BASE_URL=http://localhost:5295/api npm run dev

# For production
VITE_API_BASE_URL=https://your-api-domain.com/api npm run build
```

**Windows PowerShell:**
```powershell
# For development
$env:VITE_API_BASE_URL="http://localhost:5295/api"
npm run dev

# Or inline
$env:VITE_API_BASE_URL="http://localhost:5295/api"; npm run dev

# For production
$env:VITE_API_BASE_URL="https://your-api-domain.com/api"; npm run build
```

**Windows Command Prompt:**
```cmd
# For development
set VITE_API_BASE_URL=http://localhost:5295/api
npm run dev

# For production
set VITE_API_BASE_URL=https://your-api-domain.com/api && npm run build
```

**Default behavior:**
- If `VITE_API_BASE_URL` is set, it will be used as the API base URL
- In development mode without the variable: defaults to `http://localhost:5295/api`
- In production mode without the variable: defaults to `/api` (relative to current domain)

### Environment Files

Create these files in the project root (they are git-ignored):

```bash
# .env.local (for local development overrides)
VITE_API_BASE_URL=http://localhost:5295/api

# .env.development (for development defaults)
VITE_API_BASE_URL=http://localhost:5295/api

# .env.production (for production defaults)
VITE_API_BASE_URL=/api
```

## Development Setup

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type-aware lint rules:

```js
export default tseslint.config([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...

      // Remove tseslint.configs.recommended and replace with this
      ...tseslint.configs.recommendedTypeChecked,
      // Alternatively, use this for stricter rules
      ...tseslint.configs.strictTypeChecked,
      // Optionally, add this for stylistic rules
      ...tseslint.configs.stylisticTypeChecked,

      // Other configs...
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])
```

You can also install [eslint-plugin-react-x](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-x) and [eslint-plugin-react-dom](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-dom) for React-specific lint rules:

```js
// eslint.config.js
import reactX from 'eslint-plugin-react-x'
import reactDom from 'eslint-plugin-react-dom'

export default tseslint.config([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...
      // Enable lint rules for React
      reactX.configs['recommended-typescript'],
      // Enable lint rules for React DOM
      reactDom.configs.recommended,
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])
```
