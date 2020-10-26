export module TonApiSpec {

    export type ValueType = 'String' | 'Optional' | 'Number' | 'Boolean' | 'Ref' | 'BigInt' | 'Array';

    export interface HasValue {
        type: ValueType;
        ref_name: string;
        array_item: HasValue;
        optional_inner: HasValue;
    }

    export interface Field extends HasValue {
        name: string;
        summary: string;
        description: string;
    }

    export interface EnumConst {
        name: string;
        type: string;
        summary: string;
        description: string;
    }

    export interface Type {
        name: string;
        type: 'Struct' | 'EnumOfTypes' | 'EnumOfConsts' | 'None';
        struct_fields: Field[];
        summary: string;
        description: string;
        enum_types: Type[];
        enum_consts: EnumConst[];
    }

    export interface GenericArg {
        type: string;
        ref_name: string;
    }

    export interface Result {
        type: 'Generic' | 'Ref';
        generic_name: string;
        generic_args: GenericArg[];
    }

    export interface Param extends Result {
        name: string;
        summary?: any;
        description?: any;
        ref_name: string;
    }

    export interface Function {
        name: string;
        summary: string;
        description: string;
        params: Param[];
        result: Result;
        errors?: any;
    }

    export interface Module {
        name: string;
        summary: string;
        description: string;
        types: Type[];
        functions: Function[];
    }

    export interface RootObject {
        version: string;
        modules: Module[];
    }
}
