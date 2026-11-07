export function resolveCapabilities(capabilities, workspaces = [], activeWorkspaceId = null, user = null) {
  if (capabilities && typeof capabilities === 'object') {
    return {
      platformAdmin: !!capabilities.platformAdmin,
      schoolAdmin: !!capabilities.schoolAdmin,
      teacher: !!capabilities.teacher,
      parent: !!capabilities.parent,
      schoolMember: !!capabilities.schoolMember,
      teacherOrAbove: !!capabilities.teacherOrAbove,
      canRequestSchool: capabilities.canRequestSchool !== false,
      canManageSchool: !!capabilities.canManageSchool,
      active: {
        schoolAdmin: !!capabilities.active?.schoolAdmin,
        teacher: !!capabilities.active?.teacher,
        parent: !!capabilities.active?.parent,
        teacherOrAbove: !!capabilities.active?.teacherOrAbove,
        canManageSchool: !!capabilities.active?.canManageSchool,
        isSchool: !!capabilities.active?.isSchool,
      },
    }
  }

  const platformAdmin = Number(user?.platformRole || 0) >= 100
  const active = (workspaces || []).find((item) => item.id === activeWorkspaceId)
  const isSchool = active?.type === 'school'
  const role = String(active?.role || '').toLowerCase()
  const schoolAdminRoles = new Set(['schoolowner', 'schooladministrator'])
  const teacherOrAboveRoles = new Set(['teacher', 'academicmanager', 'schooladministrator', 'schoolowner'])

  return {
    platformAdmin,
    schoolAdmin: (workspaces || []).some((item) => item.type === 'school' && schoolAdminRoles.has(String(item.role || '').toLowerCase())),
    teacher: (workspaces || []).some((item) => item.type === 'school' && String(item.role || '').toLowerCase() === 'teacher'),
    parent: (workspaces || []).some((item) => item.type === 'school' && String(item.role || '').toLowerCase() === 'parent'),
    schoolMember: (workspaces || []).some((item) => item.type === 'school'),
    teacherOrAbove: platformAdmin || (workspaces || []).some((item) => item.type === 'school' && teacherOrAboveRoles.has(String(item.role || '').toLowerCase())),
    canRequestSchool: true,
    canManageSchool: platformAdmin || (workspaces || []).some((item) => item.type === 'school' && schoolAdminRoles.has(String(item.role || '').toLowerCase())),
    active: {
      schoolAdmin: isSchool && (platformAdmin || schoolAdminRoles.has(role)),
      teacher: isSchool && (platformAdmin || role === 'teacher' || schoolAdminRoles.has(role)),
      parent: isSchool && role === 'parent',
      teacherOrAbove: isSchool && (platformAdmin || teacherOrAboveRoles.has(role)),
      canManageSchool: isSchool && (platformAdmin || schoolAdminRoles.has(role)),
      isSchool,
    },
  }
}

export function navVisible(item, caps) {
  if (!item.requires) return true
  const need = item.requires
  if (need === 'platformAdmin') return caps.platformAdmin
  if (need === 'schoolLink') return caps.canManageSchool || caps.canRequestSchool || caps.schoolMember
  if (need === 'teacher') return caps.active.teacher || caps.teacher || caps.platformAdmin
  if (need === 'parent') return caps.active.parent || caps.parent
  if (need === 'schoolAnalytics') return caps.active.teacherOrAbove || caps.teacherOrAbove
  return true
}
