/* Copyright 2015 Realm Inc - All Rights Reserved
 * Proprietary and Confidential
 */
 
#include <realm.hpp>
#include "error_handling.hpp"
#include "marshalling.hpp"
#include "realm_export_decls.hpp"

using namespace realm;
using namespace realm::binding;

extern "C" {

REALM_EXPORT void linklist_add(LinkView* linklist_ptr, size_t row_ndx)
{
    handle_errors([&]() {
        linklist_ptr->add(row_ndx);
    });
}


REALM_EXPORT size_t linklist_size(LinkView* linklist_ptr)
{
    return handle_errors([&]() {
        return linklist_ptr->size();
    });
}


REALM_EXPORT void linklist_destroy(LinkView* linklist_ptr)
{
    return handle_errors([&]() {
      delete(linklist_ptr);
    });
}
  
}   // extern "C"
