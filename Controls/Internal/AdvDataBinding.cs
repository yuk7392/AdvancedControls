using System;
using System.Collections;
using System.ComponentModel;

namespace AdvancedControls.Controls.Internal
{
    /// <summary>
    /// 데이터 바인딩(DataSource) 공통 처리. 콤보·목록·그리드가 같은 규칙으로
    /// 원본을 IList로 풀어 쓴다 — 컨트롤마다 복제하면 형식 검사가 제각각이 된다.
    /// </summary>
    internal static class AdvDataBinding
    {
        /// <summary>
        /// DataTable은 IList가 아니라 IListSource라서 GetList()를 한 번 거쳐야 한다.
        /// </summary>
        internal static IList ResolveList(object source)
        {
            if (source == null) return null;

            var listSource = source as IListSource;
            if (listSource != null) return listSource.GetList();

            var list = source as IList;
            if (list != null) return list;

            throw new ArgumentException(
                "DataSource는 IList 또는 IListSource여야 합니다. 받은 형식: " + source.GetType().FullName);
        }
    }
}
