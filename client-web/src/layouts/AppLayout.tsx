import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import { Layout, Menu, Dropdown, Avatar, Button, Drawer, Tooltip, theme, type MenuProps } from 'antd'
import {
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UserOutlined,
  LogoutOutlined,
  LockOutlined,
  ProfileOutlined,
  SafetyOutlined,
  BulbOutlined,
  BulbFilled,
} from '@ant-design/icons'
import { useAuth } from '@/modules/identity/context/auth-context'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { navigationModules } from './navigation'

const { Header, Sider, Content } = Layout

const MOBILE_BREAKPOINT = 768
const SIDER_WIDTH = 240
const SIDER_COLLAPSED_WIDTH = 72
const HEADER_HEIGHT = 64

export function AppLayout() {
  const { t } = useTranslation()
  const { user, loginLabel, logout } = useAuth()
  const { isDark, setTheme } = usePreferences()
  const navigate = useNavigate()
  const location = useLocation()
  const { token } = theme.useToken()

  const [siderCollapsed, setSiderCollapsed] = useState(false)
  const [mobileDrawerOpen, setMobileDrawerOpen] = useState(false)
  const [isMobile, setIsMobile] = useState(
    typeof window !== 'undefined' && window.innerWidth < MOBILE_BREAKPOINT,
  )

  useEffect(() => {
    const handleResize = () => setIsMobile(window.innerWidth < MOBILE_BREAKPOINT)
    window.addEventListener('resize', handleResize)
    return () => window.removeEventListener('resize', handleResize)
  }, [])

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  const menuItems: MenuProps['items'] = navigationModules.map((mod) => ({
    key: mod.key,
    icon: mod.icon,
    label: t(mod.labelKey),
    children: mod.screens.map((screen) => ({
      key: screen.path,
      icon: screen.icon,
      label: t(screen.labelKey),
      onClick: () => {
        navigate(screen.path)
        if (isMobile) setMobileDrawerOpen(false)
      },
    })),
  }))

  const selectedKeys = navigationModules
    .flatMap((m) => m.screens)
    .filter((s) => s.path === location.pathname)
    .map((s) => s.path)

  const defaultOpenKeys = navigationModules.map((m) => m.key)

  const userMenuItems: MenuProps['items'] = [
    { key: 'profile', icon: <ProfileOutlined />, label: t('nav.profile'), onClick: () => navigate('/account') },
    {
      key: 'security',
      icon: <SafetyOutlined />,
      label: t('nav.security'),
      onClick: () => navigate('/account/security'),
    },
    {
      key: 'change-password',
      icon: <LockOutlined />,
      label: t('nav.changePassword'),
      onClick: () => navigate('/account/password'),
    },
    { type: 'divider' },
    { key: 'logout', icon: <LogoutOutlined />, label: t('nav.signOut'), danger: true, onClick: handleLogout },
  ]

  const displayName = user?.name ?? loginLabel ?? t('nav.account')

  const siderMenu = (
    <Menu
      mode="inline"
      selectedKeys={selectedKeys}
      defaultOpenKeys={defaultOpenKeys}
      items={menuItems}
      style={{ height: '100%', borderRight: 0, paddingTop: 8 }}
    />
  )

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Header
        style={{
          position: 'sticky',
          top: 0,
          zIndex: 1000,
          height: HEADER_HEIGHT,
          lineHeight: `${HEADER_HEIGHT}px`,
          padding: '0 16px',
          background: token.colorBgContainer,
          borderBottom: `1px solid ${token.colorBorderSecondary}`,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <div className="flex items-center gap-2">
          <Button
            type="text"
            icon={!isMobile && !siderCollapsed ? <MenuFoldOutlined /> : <MenuUnfoldOutlined />}
            onClick={() => (isMobile ? setMobileDrawerOpen(true) : setSiderCollapsed((c) => !c))}
            style={{ fontSize: 16 }}
          />
          <div className="flex items-center gap-2 select-none">
            <div
              className="flex items-center justify-center rounded-lg text-white font-bold text-sm"
              style={{
                width: 32,
                height: 32,
                background: `linear-gradient(135deg, ${token.colorPrimary}, ${token.colorPrimaryActive})`,
                flexShrink: 0,
              }}
            >
              P
            </div>
            <span
              className="hidden sm:block font-semibold text-base leading-none"
              style={{ color: token.colorText }}
            >
              Pandora
            </span>
          </div>
        </div>

        <div className="flex items-center gap-1">
          <Tooltip title={isDark ? t('theme.toLight') : t('theme.toDark')}>
            <Button
              type="text"
              icon={isDark ? <BulbFilled style={{ color: '#fadb14' }} /> : <BulbOutlined />}
              onClick={() => setTheme(isDark ? 'light' : 'dark')}
              style={{ fontSize: 16 }}
            />
          </Tooltip>

          <Dropdown menu={{ items: userMenuItems }} trigger={['click']} placement="bottomRight">
            <div
              className="flex items-center gap-2 cursor-pointer rounded-lg px-2 py-1 transition-colors hover:bg-black/5"
              style={{ userSelect: 'none' }}
            >
              <Avatar
                size="small"
                icon={<UserOutlined />}
                style={{ background: token.colorPrimary, flexShrink: 0 }}
              />
              <span
                className="hidden sm:block text-sm font-medium max-w-[160px] truncate"
                style={{ color: token.colorText }}
              >
                {displayName}
              </span>
            </div>
          </Dropdown>
        </div>
      </Header>

      <Layout style={{ flex: 1 }}>
        {!isMobile && (
          <Sider
            collapsed={siderCollapsed}
            width={SIDER_WIDTH}
            collapsedWidth={SIDER_COLLAPSED_WIDTH}
            style={{
              position: 'sticky',
              top: HEADER_HEIGHT,
              height: `calc(100vh - ${HEADER_HEIGHT}px)`,
              overflow: 'auto',
              flexShrink: 0,
              background: token.colorBgContainer,
              borderRight: `1px solid ${token.colorBorderSecondary}`,
            }}
          >
            {siderMenu}
          </Sider>
        )}

        {isMobile && (
          <Drawer
            placement="left"
            open={mobileDrawerOpen}
            onClose={() => setMobileDrawerOpen(false)}
            width={SIDER_WIDTH}
            styles={{ body: { padding: 0 } }}
            title={
              <div className="flex items-center gap-2">
                <div
                  className="flex items-center justify-center rounded-lg text-white font-bold text-sm"
                  style={{
                    width: 28,
                    height: 28,
                    background: `linear-gradient(135deg, ${token.colorPrimary}, ${token.colorPrimaryActive})`,
                  }}
                >
                  P
                </div>
                <span className="font-semibold">Pandora</span>
              </div>
            }
          >
            {siderMenu}
          </Drawer>
        )}

        <Content
          style={{
            padding: 24,
            background: token.colorBgLayout,
            minHeight: `calc(100vh - ${HEADER_HEIGHT}px)`,
            overflow: 'auto',
          }}
        >
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  )
}
